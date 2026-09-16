using FSH.Framework.Core.Exceptions;
using FSH.Modules.Identity.Contracts.Services;

namespace FSH.Modules.Chat.Features.v1.Internal;

internal static class ChatParticipantAccess
{
    public static async Task RequireActiveTenantUsersAsync(
        this IUserProfileService users,
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(userIds);
        var requested = userIds.Distinct(StringComparer.Ordinal).ToArray();
        var allowed = await users.GetActiveUserIdsAsync(requested, cancellationToken).ConfigureAwait(false);
        if (requested.Except(allowed, StringComparer.Ordinal).Any())
        {
            throw new NotFoundException("Participant not found.");
        }
    }
}
