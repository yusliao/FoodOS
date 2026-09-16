using FSH.Modules.Chat.Domain;
using Microsoft.AspNetCore.SignalR;

namespace FSH.Modules.Chat.Features.v1.Internal;

internal static class ChatRealtimeAudience
{
    // Channel groups can contain sockets whose membership has since been revoked.
    // Resolve recipients from the current channel aggregate instead of trusting those groups.
    public static IClientProxy CurrentMembers(this IHubClients clients, ChatChannel channel)
    {
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(channel);
        return clients.Groups(channel.Members.Select(member => $"user:{member.UserId}")
            .Distinct(StringComparer.Ordinal).ToArray());
    }
}
