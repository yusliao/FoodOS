using FSH.Modules.Files.Contracts.v1.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Files.Features.v1.ListOwnerFiles;

public static class ListOwnerFilesEndpoint
{
    internal static RouteHandlerBuilder MapListOwnerFilesEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/owners/{ownerType}/{ownerId:guid}",
                async (string ownerType, Guid ownerId, IMediator mediator, CancellationToken cancellationToken,
                    int pageNumber = 1, int pageSize = 20) =>
                    Results.Ok(await mediator.Send(new ListOwnerFilesQuery(ownerType, ownerId, pageNumber, pageSize),
                        cancellationToken).ConfigureAwait(false)))
            .WithName("ListOwnerFiles")
            .WithSummary("List authorized finalized private attachments of one owner")
            .WithDescription("Requires the owning module's explicit list policy and resource permission. Does not grant tenant-wide file access.")
            .RequireAuthorization();
}
