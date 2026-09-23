using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Chat.Contracts.Authorization;
using FSH.Modules.Chat.Contracts.v1.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Chat.Features.v1.Channels.ListArchivedChannels;

public static class ListArchivedChannelsEndpoint
{
    internal static RouteHandlerBuilder MapListArchivedChannelsEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/channels/trash",
                async (string? search, int? pageNumber, int? pageSize, IMediator mediator, CancellationToken cancellationToken) =>
                    Results.Ok(await mediator.Send(
                        new ListArchivedChannelsQuery(search, pageNumber ?? 1, pageSize ?? 20), cancellationToken)
                        .ConfigureAwait(false)))
            .WithName("ListArchivedChannels")
            .WithSummary("List archived chat channels in the current identity domain")
            .RequirePermission(ChatPermissions.Channels.ManageAll);
}
