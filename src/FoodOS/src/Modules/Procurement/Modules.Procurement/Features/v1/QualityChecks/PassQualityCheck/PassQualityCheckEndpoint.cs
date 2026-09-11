using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.QualityChecks;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.QualityChecks.PassQualityCheck;

public static class ConfirmPassQualityCheckEndpoint
{
    internal static RouteHandlerBuilder MapConfirmPassQualityCheckEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/{purchaseOrderId:guid}/lines/{lineId:guid}/qc/pass",
                async (Guid purchaseOrderId, Guid lineId, PassQualityCheckCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(
                        body with { PurchaseOrderId = purchaseOrderId, LineId = lineId }, ct).ConfigureAwait(false));
                })
            .WithName("PassQualityCheck")
            .WithSummary("Pass QC and receive stock into available inventory")
            .RequirePermission(ProcurementPermissions.Quality.Pass)
            .WithIdempotency();
    }
}
