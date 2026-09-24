using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.WmsIntegration.Contracts.Authorization;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.WmsIntegration.Features.v1.Mappings.UpsertMapping;

public static class UpsertWmsMappingEndpoint
{
    internal static RouteHandlerBuilder MapUpsertWmsMappingEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/mappings",
                async (UpsertWmsMappingCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false)))
            .WithName("UpsertWmsMapping")
            .WithSummary("Create, update, activate, or deactivate a WMS value mapping")
            .RequirePermission(WmsIntegrationPermissions.Integration.ManageMappings)
            .WithIdempotency();
    }
}
