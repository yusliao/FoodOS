using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.CreateCustomerLevel;

public static class CreateCustomerLevelEndpoint
{
    internal static RouteHandlerBuilder MapCreateCustomerLevelEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/customer-levels",
                async (CreateCustomerLevelCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateCustomerLevel")
            .WithSummary("Create a customer pricing level with a unique code and discount percent")
            .RequirePermission(CatalogPermissions.PriceLists.Create)
            .WithIdempotency();
    }
}
