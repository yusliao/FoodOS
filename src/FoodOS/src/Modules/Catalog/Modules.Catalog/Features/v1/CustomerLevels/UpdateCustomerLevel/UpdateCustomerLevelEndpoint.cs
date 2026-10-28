using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.UpdateCustomerLevel;

public static class UpdateCustomerLevelEndpoint
{
    internal static RouteHandlerBuilder MapUpdateCustomerLevelEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/customer-levels/{id:guid}",
                async (Guid id, UpdateCustomerLevelRequest request, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(
                        new UpdateCustomerLevelCommand(id, request.Code, request.Name, request.DiscountPercent),
                        ct).ConfigureAwait(false);
                    return Results.NoContent();
                })
            .WithName("UpdateCustomerLevel")
            .WithSummary("Rename a customer pricing level or change its code and discount percent")
            .RequirePermission(CatalogPermissions.PriceLists.Update);
    }
}

public sealed record UpdateCustomerLevelRequest(string Code, string Name, decimal DiscountPercent);
