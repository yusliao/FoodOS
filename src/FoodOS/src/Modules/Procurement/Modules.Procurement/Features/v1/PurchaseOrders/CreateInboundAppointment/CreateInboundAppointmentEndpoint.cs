using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateInboundAppointment;

public static class CreateInboundAppointmentEndpoint
{
    internal static RouteHandlerBuilder MapCreateInboundAppointmentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/{purchaseOrderId:guid}/appointments",
                async (Guid purchaseOrderId, CreateInboundAppointmentCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(
                        body with { PurchaseOrderId = purchaseOrderId }, ct).ConfigureAwait(false));
                })
            .WithName("CreateInboundAppointment")
            .WithSummary("Book a dock slot and move the PO into Receiving")
            .RequirePermission(ProcurementPermissions.Purchase.Create)
            .WithIdempotency();
    }
}
