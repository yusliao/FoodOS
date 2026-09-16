using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace FSH.Framework.Web.Sse;

public sealed record SsePrincipal(string UserId, string? TenantId);

public interface ISseTokenService
{
    Task<Guid> IssueAsync(string userId, string? tenantId, CancellationToken cancellationToken);

    Task<SsePrincipal?> ConsumeAsync(Guid token, CancellationToken cancellationToken);
}

/// <summary>
/// Short-lived single-use token for authenticating SSE streams. Browsers' EventSource API cannot
/// attach Authorization headers, so clients exchange their JWT at /sse/token for an opaque token,
/// then open the stream at /sse/stream?token=&lt;guid&gt;. The token is deleted on first consume and
/// expires in 30 seconds otherwise. Redis GETDEL atomically consumes across hosts; without Redis,
/// a singleton lock protects the in-memory development fallback. No fallback on Redis failure.
/// </summary>
internal sealed class SseTokenService(IMemoryCache cache, IConnectionMultiplexer? redis = null) : ISseTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromSeconds(30);
    private readonly object _memoryLock = new();

    public async Task<Guid> IssueAsync(string userId, string? tenantId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (!Guid.TryParse(userId, out var id) || id == Guid.Empty)
        {
            throw new ArgumentException("A valid user identity is required.", nameof(userId));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var token = Guid.NewGuid();
        var principal = new SsePrincipal(id.ToString(), tenantId);
        if (redis is not null)
        {
            await redis.GetDatabase().StringSetAsync(KeyFor(token), JsonSerializer.Serialize(principal), TokenLifetime)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            cache.Set(KeyFor(token), principal, TokenLifetime);
        }
        return token;
    }

    public async Task<SsePrincipal?> ConsumeAsync(Guid token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (token == Guid.Empty)
        {
            return null;
        }
        var key = KeyFor(token);
        if (redis is not null)
        {
            var payload = await redis.GetDatabase().StringGetDeleteAsync(key)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            return payload.IsNullOrEmpty ? null : JsonSerializer.Deserialize<SsePrincipal>(payload.ToString());
        }
        lock (_memoryLock)
        {
            cache.TryGetValue<SsePrincipal>(key, out var principal);
            cache.Remove(key);
            return principal;
        }
    }

    // Separate Redis string keys from the former IDistributedCache hash representation.
    private static string KeyFor(Guid token) => $"sse:tok:v2:{token:N}";
}
