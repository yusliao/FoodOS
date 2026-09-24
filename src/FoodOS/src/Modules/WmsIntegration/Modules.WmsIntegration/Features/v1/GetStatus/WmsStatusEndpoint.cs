using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.WmsIntegration.Contracts.Authorization;
using FSH.Modules.WmsIntegration.Contracts.v1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.WmsIntegration.Features.v1.GetStatus;

public static class GetWmsIntegrationStatusEndpoint
{
    internal static RouteHandlerBuilder MapWmsStatusEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/status", async (IWmsReadiness readiness, CancellationToken ct) =>
                Results.Ok(await readiness.GetSnapshotAsync(ct).ConfigureAwait(false)))
            .WithName("GetWmsIntegrationStatus")
            .WithSummary("Get the configured FoodOS WMS standard connection status")
            .RequirePermission(WmsIntegrationPermissions.Integration.View);
    }
}
