using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.WmsIntegration.Contracts.Authorization;
using FSH.Modules.WmsIntegration.Contracts.v1;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.WmsIntegration.Features.v1.GetAvailability;

public static class GetWmsAvailabilityEndpoint
{
    internal static RouteHandlerBuilder MapGetWmsAvailabilityEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/availability", (string sku, string uom, IMediator mediator, CancellationToken ct) =>
            mediator.Send(new GetWmsAvailabilityQuery(sku, uom), ct))
            .WithName("GetWmsAvailability")
            .WithSummary("Read product availability in the configured WMS warehouse")
            .RequirePermission(WmsIntegrationPermissions.Integration.View);
}
