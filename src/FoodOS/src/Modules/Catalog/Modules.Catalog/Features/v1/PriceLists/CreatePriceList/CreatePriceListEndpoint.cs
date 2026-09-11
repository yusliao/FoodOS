using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.CreatePriceList;

public static class CreatePriceListEndpoint
{
    internal static RouteHandlerBuilder MapCreatePriceListEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/price-lists",
                async (CreatePriceListCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreatePriceList")
            .WithSummary("Create a catalog or customer contract price list")
            .RequirePermission(CatalogPermissions.PriceLists.Create)
            .WithIdempotency();
    }
}
