using FSH.Framework.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Chat.Data;

internal static class ChatMessageWriteLock
{
    // Serialize message writes and read-marker advances so reply counts, tombstones,
    // and read watermarks survive concurrent requests. Hold the lock until commit.
    public static Task AcquireAsync(ChatDbContext db, string? tenantId, Guid channelId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (string.IsNullOrWhiteSpace(tenantId)) throw new UnauthorizedException("no current tenant");
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("A message write transaction is required.");
        string key = $"chat:messages:{tenantId}:{channelId:N}";
        return db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
