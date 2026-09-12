using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Ops.Contracts.Authorization;
using FSH.Modules.Ops.Contracts.v1.Trace;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ops.Features.v1.Trace.GetLotTrace;

public static class GetLotTraceEndpoint
{
    internal static RouteHandlerBuilder MapGetLotTraceEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/lots/{lotId:guid}/trace",
                (Guid lotId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetLotTraceQuery(lotId), ct))
            .WithName("GetLotTrace")
            .WithSummary("Get receiving, picking, shipping, and arrival events for a lot in time order")
            .RequirePermission(OpsPermissions.Trace.View);
    }
}
