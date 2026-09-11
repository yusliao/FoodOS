using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.GetPriceLists;

public static class GetPriceListsEndpoint
{
    internal static RouteHandlerBuilder MapGetPriceListsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/price-lists",
                (Guid? customerOrgId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetPriceListsQuery(customerOrgId), ct))
            .WithName("GetPriceLists")
            .WithSummary("List price lists; pass customerOrgId to return only that customer's contracts")
            .RequirePermission(CatalogPermissions.PriceLists.View);
    }
}
