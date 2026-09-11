using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Suppliers;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Suppliers.SearchSuppliers;

public static class SearchSuppliersEndpoint
{
    internal static RouteHandlerBuilder MapSearchSuppliersEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/suppliers",
                (string? search, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchSuppliersQuery(search), ct))
            .WithName("SearchSuppliers")
            .WithSummary("Search suppliers")
            .RequirePermission(ProcurementPermissions.Suppliers.View);
    }
}
