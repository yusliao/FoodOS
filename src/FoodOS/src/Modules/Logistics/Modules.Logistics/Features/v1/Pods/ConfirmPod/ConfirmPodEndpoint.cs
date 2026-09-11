using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Contracts.v1.ProofOfDelivery;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Logistics.Features.v1.Pods.ConfirmPod;

public static class ConfirmPodEndpoint
{
    internal static RouteHandlerBuilder MapConfirmPodEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/stops/{stopId:guid}/pod",
                async (Guid stopId, ConfirmPodCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(
                        body with { StopId = stopId }, ct).ConfigureAwait(false));
                })
            .WithName("ConfirmPod")
            .WithSummary("Record electronic proof of delivery and optional on-truck returns")
            .RequirePermission(LogisticsPermissions.ProofOfDelivery.Confirm)
            .WithIdempotency();
    }
}
