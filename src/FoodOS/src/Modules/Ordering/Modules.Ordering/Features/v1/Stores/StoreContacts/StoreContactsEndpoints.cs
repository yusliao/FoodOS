using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Stores.StoreContacts;

public static class StoreContactsEndpoints
{
    internal static void MapStoreContactsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/stores/{storeId:guid}/contacts",
                (Guid storeId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetStoreContactsQuery(storeId), ct))
            .WithName("GetStoreContacts")
            .WithSummary("List the caller contacts used to recognise a store's phone orders")
            .RequirePermission(OrderingPermissions.Stores.View);

        endpoints.MapPut("/stores/{storeId:guid}/contacts",
                (Guid storeId, ReplaceStoreContactsRequest request, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new ReplaceStoreContactsCommand(storeId, request.Contacts), ct))
            .WithName("ReplaceStoreContacts")
            .WithSummary("Replace a store's caller contacts")
            .RequirePermission(OrderingPermissions.Stores.Update);
    }
}

internal sealed record ReplaceStoreContactsRequest(IReadOnlyList<StoreContactInput> Contacts);
