using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ops.Contracts.Authorization;
using FSH.Modules.Ops.Contracts.v1.Kpis;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ops.Features.v1.Kpis.GetOpsKpis;

public static class GetOpsKpisEndpoint
{
    internal static RouteHandlerBuilder MapGetOpsKpisEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/kpis",
                (DateOnly? date, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetOpsKpisQuery(date), ct))
            .WithName("GetOpsKpis")
            .WithSummary("Get daily fulfillment, stockout, shrinkage, and temperature KPIs")
            .RequirePermission(OpsPermissions.Kpis.View);
    }
}
