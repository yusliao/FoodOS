using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Chat.Contracts.Authorization;
using FSH.Modules.Chat.Contracts.v1.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Chat.Features.v1.Messages.GetChannelMessage;

public static class GetChannelMessageEndpoint
{
    internal static RouteHandlerBuilder MapGetChannelMessageEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/channels/{channelId:guid}/messages/{messageId:guid}",
                async (Guid channelId, Guid messageId, IMediator mediator, CancellationToken cancellationToken) =>
                    Results.Ok(await mediator.Send(
                        new GetChannelMessageQuery(channelId, messageId), cancellationToken).ConfigureAwait(false)))
            .WithName("GetChannelMessage")
            .WithSummary("Locate a message or thread reply within a channel the caller has joined")
            .RequirePermission(ChatPermissions.Channels.View);
}
