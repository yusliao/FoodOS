using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.QualityChecks;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.QualityChecks.FailQualityCheck;

public static class ConfirmFailQualityCheckEndpoint
{
    internal static RouteHandlerBuilder MapConfirmFailQualityCheckEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/{purchaseOrderId:guid}/lines/{lineId:guid}/qc/fail",
                async (Guid purchaseOrderId, Guid lineId, FailQualityCheckCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(
                        body with { PurchaseOrderId = purchaseOrderId, LineId = lineId }, ct).ConfigureAwait(false));
                })
            .WithName("FailQualityCheck")
            .WithSummary("Fail QC and receive stock as isolated (ATP unchanged)")
            .RequirePermission(ProcurementPermissions.Quality.Fail)
            .WithIdempotency();
    }
}
