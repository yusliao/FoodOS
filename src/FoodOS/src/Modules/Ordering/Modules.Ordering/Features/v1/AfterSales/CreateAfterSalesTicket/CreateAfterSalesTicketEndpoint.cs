using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.AfterSales;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.AfterSales.CreateAfterSalesTicket;

public static class CreateAfterSalesTicketEndpoint
{
    internal static RouteHandlerBuilder MapCreateAfterSalesTicketEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/after-sales",
                async (CreateAfterSalesTicketCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("CreateAfterSalesTicket")
            .WithSummary("File a shortage, damage, or return claim against a received order")
            .RequirePermission(OrderingPermissions.Shop.Order)
            .WithIdempotency();
    }
}
