using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Access;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Features.v1.Carts.GetCart;
using FSH.Modules.Ordering.Features.v1.Carts.UpdateCart;
using FSH.Modules.Ordering.Features.v1.CustomerOrgs.CreateCustomerOrg;
using FSH.Modules.Ordering.Features.v1.CustomerOrgs.SearchCustomerOrgs;
using FSH.Modules.Ordering.Features.v1.AfterSales.CreateAfterSalesTicket;
using FSH.Modules.Ordering.Features.v1.AfterSales.SearchAfterSalesTickets;
using FSH.Modules.Ordering.Features.v1.Orders.AmendOrder;
using FSH.Modules.Ordering.Features.v1.Orders.CancelOrder;
using FSH.Modules.Ordering.Features.v1.Orders.GetOrderById;
using FSH.Modules.Ordering.Features.v1.Orders.PlaceOrder;
using FSH.Modules.Ordering.Features.v1.Orders.ReconcileOrder;
using FSH.Modules.Ordering.Features.v1.Orders.SearchOrders;
using FSH.Modules.Ordering.Features.v1.Stores.CreateStore;
using FSH.Modules.Ordering.Features.v1.Stores.GetStoreById;
using FSH.Modules.Ordering.Features.v1.Stores.GetStores;
using FSH.Modules.Ordering.Features.v1.StoreAccess.GetMyStoreAccess;
using FSH.Modules.Ordering.Features.v1.StoreAccess.SetUserStoreAccess;
using FSH.Modules.Ordering.Features.v1.Shop.SearchShopProducts;
using FSH.Modules.Ordering.Features.v1.Shop.GetMyStores;
using FSH.Modules.Ordering.Features.v1.Shop.ShopCartOrders;
using FSH.Modules.Ordering.Features.v1.Shop.ShopAfterSales;
using FSH.Modules.Ordering.Features.v1.Shop.SearchShopDeliveries;
using FSH.Modules.Ordering.Jobs;
using FSH.Modules.Ordering.Services;
using Hangfire;
using Hangfire.Common;
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
        builder.Services.AddScoped<ICustomerAccessScopeResolver, CustomerAccessScopeResolver>();
        builder.Services.AddScoped<ICustomerDeliveryNotificationAudience, CustomerDeliveryNotificationAudience>();
        builder.Services.AddScoped<IWarehouseOrderFeedbackSink, WarehouseOrderFeedbackSink>();
        builder.Services.AddScoped<IDbInitializer, OrderingDbInitializer>();
        builder.Services.AddTransient<ReconcileReminderJob>();
        builder.Services.AddTransient<WmsOrderNotificationJob>();

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
        group.MapGetMyStoreAccessEndpoint();
        group.MapSetUserStoreAccessEndpoint();
        group.MapGetCartEndpoint();
        group.MapUpdateCartEndpoint();
        group.MapSearchOrdersEndpoint();
        group.MapPlaceOrderEndpoint();
        group.MapAmendOrderEndpoint();
        group.MapCancelOrderEndpoint();
        group.MapGetOrderByIdEndpoint();
        group.MapConfirmReconcileOrderEndpoint();
        group.MapSearchAfterSalesTicketsEndpoint();
        group.MapCreateAfterSalesTicketEndpoint();

        var shopGroup = endpoints
            .MapGroup("api/v{version:apiVersion}/shop")
            .WithTags("Shop")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();
        shopGroup.MapSearchShopProductsEndpoint();
        shopGroup.MapGetShopProductByIdEndpoint();
        shopGroup.MapGetMyStoresEndpoint();
        shopGroup.MapGetMyStoreByIdEndpoint();
        shopGroup.MapShopCartOrdersEndpoints();
        shopGroup.MapShopAfterSalesEndpoints();
        shopGroup.MapSearchShopDeliveriesEndpoint();

        var jobManager = endpoints.ServiceProvider.GetService<IRecurringJobManager>();
        if (jobManager is not null)
        {
            jobManager.AddOrUpdate(
                "ordering-reconcile-reminder",
                Job.FromExpression<ReconcileReminderJob>(j => j.RunAsync(CancellationToken.None)),
                "* * * * *",
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
            jobManager.AddOrUpdate(
                "ordering-wms-order-notification",
                Job.FromExpression<WmsOrderNotificationJob>(j => j.RunAsync(CancellationToken.None)),
                "* * * * *",
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }
}
