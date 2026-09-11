using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.Products;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.Products.UpsertProductTranslation;

public static class UpsertProductTranslationEndpoint
{
    internal static RouteHandlerBuilder MapUpsertProductTranslationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/products/{productId:guid}/translations/{culture}",
                async (Guid productId, string culture, UpsertProductTranslationBody body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(
                        new UpsertProductTranslationCommand(productId, culture, body.Name, body.Description),
                        ct).ConfigureAwait(false));
                })
            .WithName("UpsertProductTranslation")
            .WithSummary("Create or update a localized product name")
            .RequirePermission(CatalogPermissions.Products.Update)
            .WithIdempotency();
    }
}
