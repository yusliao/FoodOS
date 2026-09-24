using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.WmsIntegration.Contracts.Authorization;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.WmsIntegration.Features.v1.Mappings.SearchMappings;

public static class SearchWmsMappingsEndpoint
{
    internal static RouteHandlerBuilder MapSearchWmsMappingsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/mappings",
                (string? kind, string? search, bool? isActive, int pageNumber, int pageSize,
                    IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchWmsMappingsQuery(
                        kind,
                        search,
                        isActive,
                        pageNumber < 1 ? 1 : pageNumber,
                        pageSize < 1 ? 20 : pageSize), ct))
            .WithName("SearchWmsMappings")
            .WithSummary("Search mappings for the configured WMS connection")
            .RequirePermission(WmsIntegrationPermissions.Integration.View);
    }
}
