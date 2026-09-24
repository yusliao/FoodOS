using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.WmsIntegration.Contracts.Authorization;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.WmsIntegration.Features.v1.Mappings.ValidateMappings;

public static class ValidateWmsMappingsEndpoint
{
    internal static RouteHandlerBuilder MapValidateWmsMappingsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/mappings/validate",
                async (ValidateWmsMappingsQuery query, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(query, ct).ConfigureAwait(false)))
            .WithName("ValidateWmsMappings")
            .WithSummary("Resolve required mappings and report missing or inactive values")
            .RequirePermission(WmsIntegrationPermissions.Integration.View);
    }
}
