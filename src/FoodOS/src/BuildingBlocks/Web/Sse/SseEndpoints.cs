using FSH.Framework.Core.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Framework.Web.Sse;

public static class SseEndpoints
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Maps the SSE token exchange endpoint (<c>POST /api/v1/sse/token</c>, authenticated via JWT) and
    /// the streaming endpoint (<c>GET /api/v1/sse/stream?token=&lt;guid&gt;</c>). Browsers' EventSource
    /// API cannot send an Authorization header, so the stream authenticates via a short-lived opaque
    /// token issued from the token endpoint.
    /// </summary>
    public static IEndpointRouteBuilder MapHeroSseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/v1/sse/token", async (
            HttpContext context,
            ICurrentUser currentUser,
            ISseTokenService tokens,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.IsAuthenticated() || currentUser.GetUserId() == Guid.Empty
                || string.IsNullOrWhiteSpace(currentUser.GetTenant()))
            {
                return Results.Unauthorized();
            }

            var userId = currentUser.GetUserId().ToString();
            var tenantId = currentUser.GetTenant();
            var token = await tokens.IssueAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token });
        })
        .WithName("SseToken")
        .WithSummary("Issue a short-lived SSE stream token")
        .WithTags("SSE")
        .RequireAuthorization();

        endpoints.MapGet("/api/v1/sse/stream", async (
            HttpContext context,
            [Microsoft.AspNetCore.Mvc.FromQuery] Guid token,
            ISseTokenService tokens,
            SseConnectionManager connectionManager,
            CancellationToken cancellationToken) =>
        {
            var principal = await tokens.ConsumeAsync(token, cancellationToken).ConfigureAwait(false);
            if (principal is null || string.IsNullOrWhiteSpace(principal.TenantId)
                || !Guid.TryParse(principal.UserId, out var userId) || userId == Guid.Empty)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-store";
            // No `Connection: keep-alive` — it's a hop-by-hop header forbidden on HTTP/2+ (RFC 9113 §8.2.2),
            // so Kestrel strips it and warns on every SSE connect (the feed serves over HTTP/2 via ALPN).
            // It was redundant anyway: HTTP/1.1 keeps connections alive by default.
            context.Response.Headers["X-Accel-Buffering"] = "no"; // disable nginx buffering

            var (connectionId, reader) = connectionManager.Connect(userId.ToString(), principal.TenantId);

            // Flush the response headers + an initial comment immediately. Kestrel buffers
            // response headers until the first body write, and our first write would otherwise
            // be the heartbeat up to HeartbeatInterval (15s) away — so the client's fetch()
            // promise (which resolves on response headers) would sit pending and the UI would
            // show "connecting" for up to 15s on every connect/reconnect. Writing a no-op SSE
            // comment now sends the headers and lets the client flip to "connected" at once.
            using var heartbeat = new PeriodicTimer(HeartbeatInterval);
            using var streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var streamToken = streamCancellation.Token;

            try
            {
                await context.Response.WriteAsync(":connected\n\n", streamToken).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(streamToken).ConfigureAwait(false);
                Task<bool>? waitTask = null;
                Task<bool>? tickTask = null;
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Keep the losing wait: PeriodicTimer permits only one pending consumer.
                    waitTask ??= reader.WaitToReadAsync(streamToken).AsTask();
                    tickTask ??= heartbeat.WaitForNextTickAsync(streamToken).AsTask();

                    var completed = await Task.WhenAny(waitTask, tickTask).ConfigureAwait(false);

                    if (completed == tickTask)
                    {
                        if (!await tickTask.ConfigureAwait(false))
                        {
                            break;
                        }
                        tickTask = null;
                        await context.Response.WriteAsync(":heartbeat\n\n", cancellationToken).ConfigureAwait(false);
                        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var hasData = await waitTask.ConfigureAwait(false);
                    waitTask = null;
                    if (!hasData)
                    {
                        break;
                    }

                    while (reader.TryRead(out var sseEvent))
                    {
                        await WriteEventAsync(context.Response, sseEvent, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client disconnected — expected.
            }
            finally
            {
                await streamCancellation.CancelAsync().ConfigureAwait(false);
                connectionManager.Disconnect(connectionId);
            }
        })
        .WithName("SseStream")
        .WithSummary("Server-Sent Events stream (authenticates via ?token= issued from /sse/token)")
        .WithTags("SSE")
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
        .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task WriteEventAsync(HttpResponse response, SseEvent sseEvent, CancellationToken ct)
    {
        if (sseEvent.Id is not null)
        {
            await response.WriteAsync($"id: {sseEvent.Id}\n", ct).ConfigureAwait(false);
        }

        await response.WriteAsync($"event: {sseEvent.EventType}\n", ct).ConfigureAwait(false);

        // SSE spec: multi-line data needs each line prefixed with "data: "
        foreach (var line in sseEvent.Data.Split('\n'))
        {
            await response.WriteAsync($"data: {line}\n", ct).ConfigureAwait(false);
        }

        await response.WriteAsync("\n", ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }
}
