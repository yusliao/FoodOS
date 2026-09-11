using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Catalog.Contracts.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.Products.AdjustProductStock;

public static class AdjustProductStockEndpoint
{
    internal const string GoneDetail =
        "Catalog product stock is deprecated. Available quantity is managed by Inventory.";

    internal static RouteHandlerBuilder MapAdjustProductStockEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPatch("/products/{productId:guid}/stock",
                (Guid productId) => Results.Problem(
                    title: "Gone",
                    detail: GoneDetail,
                    statusCode: StatusCodes.Status410Gone,
                    instance: $"/api/v1/catalog/products/{productId}/stock"))
            .WithName("AdjustProductStock")
            .WithSummary("Deprecated. Returns 410 Gone. Available quantity is managed by Inventory.")
            .RequirePermission(CatalogPermissions.Products.AdjustStock);
    }
}
