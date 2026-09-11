using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.Products;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.Products.UpdateProductFulfillment;

public static class UpdateProductFulfillmentEndpoint
{
    internal static RouteHandlerBuilder MapUpdateProductFulfillmentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/products/{productId:guid}/fulfillment",
                async (Guid productId, UpdateProductFulfillmentCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(command);
                    var body = command with { ProductId = productId };
                    return Results.Ok(await mediator.Send(body, ct).ConfigureAwait(false));
                })
            .WithName("UpdateProductFulfillment")
            .WithSummary("Update foodservice fulfillment attributes")
            .RequirePermission(CatalogPermissions.Products.Update);
    }
}
