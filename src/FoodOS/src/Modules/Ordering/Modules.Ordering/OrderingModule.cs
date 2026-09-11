using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Features.v1.Carts.GetCart;
using FSH.Modules.Ordering.Features.v1.Carts.UpdateCart;
using FSH.Modules.Ordering.Features.v1.CustomerOrgs.CreateCustomerOrg;
using FSH.Modules.Ordering.Features.v1.CustomerOrgs.SearchCustomerOrgs;
using FSH.Modules.Ordering.Features.v1.Orders.AmendOrder;
using FSH.Modules.Ordering.Features.v1.Orders.CancelOrder;
using FSH.Modules.Ordering.Features.v1.Orders.GetOrderById;
using FSH.Modules.Ordering.Features.v1.Orders.PlaceOrder;
using FSH.Modules.Ordering.Features.v1.Orders.ReconcileOrder;
using FSH.Modules.Ordering.Features.v1.Orders.SearchOrders;
using FSH.Modules.Ordering.Features.v1.Stores.CreateStore;
using FSH.Modules.Ordering.Features.v1.Stores.GetStoreById;
using FSH.Modules.Ordering.Features.v1.Stores.GetStores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Ordering.OrderingModule), 660)]

namespace FSH.Modules.Ordering;

public sealed class OrderingModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(OrderingPermissions.All);

        builder.Services.AddHeroDbContext<OrderingDbContext>();
        builder.Services.AddScoped<IDbInitializer, OrderingDbInitializer>();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<OrderingDbContext>(
                name: "db:ordering",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        // No custom middleware needed
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/ordering")
            .WithTags("Ordering")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapSearchCustomerOrgsEndpoint();
        group.MapCreateCustomerOrgEndpoint();
        group.MapGetStoresEndpoint();
        group.MapCreateStoreEndpoint();
        group.MapGetStoreByIdEndpoint();
        group.MapGetCartEndpoint();
        group.MapUpdateCartEndpoint();
        group.MapSearchOrdersEndpoint();
        group.MapPlaceOrderEndpoint();
        group.MapAmendOrderEndpoint();
        group.MapCancelOrderEndpoint();
        group.MapGetOrderByIdEndpoint();
        group.MapConfirmReconcileOrderEndpoint();
    }
}
