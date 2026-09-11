using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Inventory.Contracts.Authorization;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Features.v1.Stock.GetAvailableQty;
using FSH.Modules.Inventory.Features.v1.Stock.IsolateStock;
using FSH.Modules.Inventory.Features.v1.Stock.ReceiveInventory;
using FSH.Modules.Inventory.Features.v1.Stock.ReserveStock;
using FSH.Modules.Inventory.Features.v1.Stock.UnreserveStock;
using FSH.Modules.Inventory.Features.v1.Warehouses.CreateWarehouse;
using FSH.Modules.Inventory.Features.v1.Warehouses.GetWarehouseById;
using FSH.Modules.Inventory.Features.v1.Warehouses.SearchWarehouses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Inventory.InventoryModule), 650)]

namespace FSH.Modules.Inventory;

public sealed class InventoryModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(InventoryPermissions.All);

        builder.Services.AddHeroDbContext<InventoryDbContext>();
        builder.Services.AddScoped<IDbInitializer, InventoryDbInitializer>();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<InventoryDbContext>(
                name: "db:inventory",
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
            .MapGroup("api/v{version:apiVersion}/inventory")
            .WithTags("Inventory")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapSearchWarehousesEndpoint();
        group.MapCreateWarehouseEndpoint();
        group.MapGetWarehouseByIdEndpoint();
        group.MapReceiveInventoryEndpoint();
        group.MapGetAvailableQtyEndpoint();
        group.MapReserveStockEndpoint();
        group.MapUnreserveStockEndpoint();
        group.MapIsolateStockEndpoint();
    }
}
