using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FSH.Framework.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FSH.Framework.Web.Idempotency;

/// <summary>
/// Endpoint filter that provides idempotency for POST/PUT/PATCH requests.
/// When an Idempotency-Key header is present, the response is cached and replayed
/// for subsequent requests with the same key.
/// </summary>
/// <remarks>
/// Uses <see cref="IDistributedCache"/> for both reads and writes. HybridCache stores an
/// implementation-specific envelope and may transform the underlying key, so its entries cannot
/// be probed as raw JSON through <see cref="IDistributedCache"/>.
/// </remarks>
public sealed class IdempotencyEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;
        var options = httpContext.RequestServices.GetRequiredService<IOptions<IdempotencyOptions>>().Value;
        var idempotencyKey = httpContext.Request.Headers[options.HeaderName].ToString();

        // No header = pass through (idempotency is opt-in per request)
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return await next(context).ConfigureAwait(false);
        }

        if (idempotencyKey.Length > options.MaxKeyLength)
        {
            return TypedResults.BadRequest($"Idempotency key exceeds maximum length of {options.MaxKeyLength}.");
        }

        var distributedCache = httpContext.RequestServices.GetRequiredService<IDistributedCache>();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<IdempotencyEndpointFilter>>();
        var jsonOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;

        // Include tenant context in cache key for isolation
        var tenantId = httpContext.User.FindFirst("tenant")?.Value ?? "global";
        var cacheKey = CacheKeys.IdempotencyEntry(tenantId, idempotencyKey);

        var cachedBytes = await distributedCache.GetAsync(cacheKey, httpContext.RequestAborted).ConfigureAwait(false);
        if (cachedBytes is not null && cachedBytes.Length > 0)
        {
            var cached = JsonSerializer.Deserialize<CachedIdempotentResponse>(cachedBytes, jsonOptions);
            if (cached is not null)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Idempotent replay for key {KeyHash}", HashKey(idempotencyKey));
                }
                httpContext.Response.Headers["Idempotency-Replayed"] = "true";
                httpContext.Response.StatusCode = cached.StatusCode;
                if (cached.ContentType is not null)
                {
                    httpContext.Response.ContentType = cached.ContentType;
                }

                if (cached.Body.Length > 0)
                {
                    await httpContext.Response.Body.WriteAsync(cached.Body, httpContext.RequestAborted).ConfigureAwait(false);
                }

                return TypedResults.Empty; // Response already written
            }
        }

        // Execute the handler
        var result = await next(context).ConfigureAwait(false);

        try
        {
            var responseValue = result switch
            {
                IValueHttpResult valueResult => valueResult.Value,
                IResult => null,
                _ => result,
            };
            var body = responseValue is not null
                ? JsonSerializer.SerializeToUtf8Bytes(responseValue, responseValue.GetType(), jsonOptions)
                : [];
            var statusCode = result is IStatusCodeHttpResult statusCodeResult
                ? statusCodeResult.StatusCode
                : httpContext.Response.StatusCode;
            var contentType = result is IContentTypeHttpResult contentTypeResult
                ? contentTypeResult.ContentType
                : httpContext.Response.ContentType;
            var responseToCache = new CachedIdempotentResponse
            {
                StatusCode = statusCode is > 0 and < 600 ? statusCode.Value : StatusCodes.Status200OK,
                ContentType = contentType ?? "application/json",
                Body = body
            };

            var cacheEntryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = options.DefaultTtl,
            };
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(responseToCache, jsonOptions);
            await distributedCache.SetAsync(cacheKey, responseBytes, cacheEntryOptions, httpContext.RequestAborted).ConfigureAwait(false);
        }
        // Best-effort caching: idempotency replay is a convenience, not a correctness requirement
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to cache idempotent response for key {KeyHash}", HashKey(idempotencyKey));
        }

        return result;
    }

    private static string HashKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}

public static class IdempotencyEndpointExtensions
{
    /// <summary>
    /// Enables idempotency for this endpoint. Requires Idempotency-Key header on requests.
    /// Duplicate requests with the same key return the cached response.
    /// </summary>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter<IdempotencyEndpointFilter>();
    }
}
