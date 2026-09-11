using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using FSH.Modules.Identity.Domain;
using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Warehouse.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CatalogTemperature = FSH.Modules.Catalog.Domain.TemperatureZone;
using LogisticsDriver = FSH.Modules.Logistics.Domain.Driver;
using LogisticsRoute = FSH.Modules.Logistics.Domain.Route;
using LogisticsVehicle = FSH.Modules.Logistics.Domain.Vehicle;
using ProcurementSupplier = FSH.Modules.Procurement.Domain.Supplier;
using ProcurementTrace = FSH.Modules.Procurement.Domain.TraceEvent;
using WarehouseLocation = FSH.Modules.Warehouse.Domain.Location;
using WarehouseLocationType = FSH.Modules.Warehouse.Domain.LocationType;

namespace FoodOS.DbMigrator.DemoSeed;

/// <summary>
/// Acme-only FoodOS demo chain: one DC, three zones, locations, supplier lots,
/// two customer orgs × two stores, one truck / driver / route. Idempotent.
/// </summary>
internal static class FoodOsOperationalSeeder
{
    internal const string WarehouseCode = "BOS1";
    internal const string SupplierCode = "HARBOR";
    internal const string VehiclePlate = "BOS-001";
    internal const string RouteCode = "R01";
    internal const string DriverEmail = "driver@acme.com";

    public static async Task SeedAcmeAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var tenantStore = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var tenant = await tenantStore.GetAsync(DemoSeeder.Acme.Id).ConfigureAwait(false);
        if (tenant is null)
        {
            return;
        }

        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        var inventory = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var warehouseDb = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var procurement = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var ordering = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var logistics = scope.ServiceProvider.GetRequiredService<LogisticsDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();

        var warehouse = await EnsureWarehouseAsync(inventory, logger, cancellationToken).ConfigureAwait(false);
        await EnsureLocationsAsync(warehouseDb, warehouse, logger, cancellationToken).ConfigureAwait(false);
        var supplier = await EnsureSupplierAsync(procurement, logger, cancellationToken).ConfigureAwait(false);
        await EnsureLotsAsync(inventory, procurement, catalog, warehouse, supplier.Id, logger, cancellationToken)
            .ConfigureAwait(false);
        var stores = await EnsureCustomersAndStoresAsync(ordering, warehouse.Id, logger, cancellationToken)
            .ConfigureAwait(false);
        await EnsureContractPricesAsync(catalog, ordering, logger, cancellationToken).ConfigureAwait(false);
        await EnsureLogisticsAsync(logistics, users, warehouse.Id, stores, logger, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<Warehouse> EnsureWarehouseAsync(
        InventoryDbContext inventory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existing = await inventory.Warehouses
            .FirstOrDefaultAsync(w => w.Code == WarehouseCode, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var warehouse = Warehouse.Create(
            WarehouseCode,
            "Boston DC",
            "Boston",
            OperatingClock.Default("America/New_York"));
        inventory.Warehouses.Add(warehouse);
        await inventory.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "[demo-seed] [{Tenant}] warehouse {Code} with 3 temperature zones (cutoff 16:00 {Tz})",
                DemoSeeder.Acme.Id, warehouse.Code, warehouse.TimeZoneId);
        }
        return warehouse;
    }

    private static async Task EnsureLocationsAsync(
        WarehouseDbContext warehouseDb,
        Warehouse warehouse,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        int added = 0;
        foreach (var zone in warehouse.Zones)
        {
            string prefix = LocationPrefix(zone.Kind);
            added += await AddLocationIfMissingAsync(
                warehouseDb, warehouse.Id, zone.Id, $"{prefix}-A01", WarehouseLocationType.Storage, cancellationToken)
                .ConfigureAwait(false);
            added += await AddLocationIfMissingAsync(
                warehouseDb, warehouse.Id, zone.Id, $"{prefix}-A02", WarehouseLocationType.Storage, cancellationToken)
                .ConfigureAwait(false);
            added += await AddLocationIfMissingAsync(
                warehouseDb, warehouse.Id, zone.Id, $"{prefix}-PICK", WarehouseLocationType.Pick, cancellationToken)
                .ConfigureAwait(false);
            added += await AddLocationIfMissingAsync(
                warehouseDb, warehouse.Id, zone.Id, $"{prefix}-DOCK", WarehouseLocationType.Dock, cancellationToken)
                .ConfigureAwait(false);
            added += await AddLocationIfMissingAsync(
                warehouseDb, warehouse.Id, zone.Id, $"{prefix}-Q01", WarehouseLocationType.Quarantine, cancellationToken)
                .ConfigureAwait(false);
        }

        if (added == 0)
        {
            return;
        }

        await warehouseDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[demo-seed] [{Tenant}] seeded {Count} warehouse locations", DemoSeeder.Acme.Id, added);
        }
    }

    private static async Task<int> AddLocationIfMissingAsync(
        WarehouseDbContext warehouseDb,
        Guid warehouseId,
        Guid zoneId,
        string code,
        WarehouseLocationType type,
        CancellationToken cancellationToken)
    {
        string normalized = code.Trim().ToUpperInvariant();
        bool exists = await warehouseDb.Locations
            .AnyAsync(l => l.WarehouseId == warehouseId && l.Code == normalized, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return 0;
        }

        warehouseDb.Locations.Add(WarehouseLocation.Create(warehouseId, zoneId, normalized, type));
        return 1;
    }

    private static async Task<ProcurementSupplier> EnsureSupplierAsync(
        ProcurementDbContext procurement,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existing = await procurement.Suppliers
            .FirstOrDefaultAsync(s => s.Code == SupplierCode, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var supplier = ProcurementSupplier.Create(SupplierCode, "Harbor Foods", "Produce,Dairy,Frozen,Grocery", 2);
        procurement.Suppliers.Add(supplier);
        await procurement.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[demo-seed] [{Tenant}] supplier {Code}", DemoSeeder.Acme.Id, supplier.Code);
        }
        return supplier;
    }

    private static async Task EnsureLotsAsync(
        InventoryDbContext inventory,
        ProcurementDbContext procurement,
        CatalogDbContext catalog,
        Warehouse warehouse,
        Guid supplierId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var foodSkus = CatalogSeedData.ProductSpecs
            .Where(s => s.BrandName is "FreshLine" or "Harbor Dairy" or "Arctic Pack" or "Pantry Co")
            .Select(s => s.Sku.Trim().ToUpperInvariant())
            .ToList();

        var products = await catalog.Products
            .Where(p => foodSkus.Contains(p.Sku))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (products.Count == 0)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning("[demo-seed] [{Tenant}] no food SKUs in catalog — skip lots", DemoSeeder.Acme.Id);
            }
            return;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        int lotsAdded = 0;

        foreach (var product in products)
        {
            if (string.Equals(product.Sku, "HD-ML-501", StringComparison.OrdinalIgnoreCase))
            {
                lotsAdded += await ReceiveIfMissingAsync(
                    inventory, procurement, warehouse, product, supplierId,
                    "DEMO-ML-NEAR", today.AddDays(5), 40m, isolated: false, cancellationToken)
                    .ConfigureAwait(false);
                lotsAdded += await ReceiveIfMissingAsync(
                    inventory, procurement, warehouse, product, supplierId,
                    "DEMO-ML-FAR", today.AddDays(21), 80m, isolated: false, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (string.Equals(product.Sku, "FL-SP-402", StringComparison.OrdinalIgnoreCase))
            {
                lotsAdded += await ReceiveIfMissingAsync(
                    inventory, procurement, warehouse, product, supplierId,
                    "DEMO-SP-OK", today.AddDays(8), 60m, isolated: false, cancellationToken)
                    .ConfigureAwait(false);
                lotsAdded += await ReceiveIfMissingAsync(
                    inventory, procurement, warehouse, product, supplierId,
                    "DEMO-SP-ISO", today.AddDays(6), 25m, isolated: true, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            string lotNo = $"DEMO-{product.Sku}";
            decimal qty = 80m;
            DateOnly expiry = product.TemperatureZone switch
            {
                CatalogTemperature.Chilled => today.AddDays(14),
                CatalogTemperature.Frozen => today.AddDays(180),
                _ => today.AddDays(360)
            };
            lotsAdded += await ReceiveIfMissingAsync(
                inventory, procurement, warehouse, product, supplierId,
                lotNo, expiry, qty, isolated: false, cancellationToken)
                .ConfigureAwait(false);
        }

        if (lotsAdded == 0)
        {
            return;
        }

        await inventory.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await procurement.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "[demo-seed] [{Tenant}] received {Lots} QC-passed lots (staggered expiry + 1 isolated)",
                DemoSeeder.Acme.Id, lotsAdded);
        }
    }

    private static async Task<int> ReceiveIfMissingAsync(
        InventoryDbContext inventory,
        ProcurementDbContext procurement,
        Warehouse warehouse,
        Product product,
        Guid supplierId,
        string lotNo,
        DateOnly expiry,
        decimal qty,
        bool isolated,
        CancellationToken cancellationToken)
    {
        string normalized = lotNo.Trim().ToUpperInvariant();
        bool exists = await inventory.Lots
            .AnyAsync(l => l.LotNo == normalized && l.ProductId == product.Id, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return 0;
        }

        var zone = warehouse.ZoneOf(ToKind(product.TemperatureZone));
        var lot = Lot.Create(
            normalized,
            product.Id,
            expiry,
            manufacturedOn: expiry.AddDays(-10),
            supplierId: supplierId,
            origin: "Boston");
        inventory.Lots.Add(lot);

        var balance = LotBalance.Create(warehouse.Id, zone.Id, lot.Id, product.Id);
        if (isolated)
        {
            balance.ReceiveIsolated(qty);
            if (balance.IsFullyIsolated)
            {
                lot.Isolate();
            }
        }
        else
        {
            balance.Receive(qty);
        }

        inventory.LotBalances.Add(balance);

        string key = $"demo-recv:{normalized}:{product.Sku}";
        inventory.InventoryTransactions.Add(InventoryTransaction.Create(
            InventoryTransactionType.Receive,
            product.Id,
            warehouse.Id,
            zone.Id,
            qty,
            key,
            lot.Id,
            fromBucket: null,
            toBucket: InventoryBucket.OnHand,
            refType: isolated ? "ReceiveIsolated" : "Receive",
            refId: lot.Id));

        if (isolated)
        {
            inventory.InventoryTransactions.Add(InventoryTransaction.Create(
                InventoryTransactionType.Isolate,
                product.Id,
                warehouse.Id,
                zone.Id,
                qty,
                $"{key}:isolate",
                lot.Id,
                fromBucket: InventoryBucket.OnHand,
                toBucket: InventoryBucket.Isolated,
                refType: "ReceiveIsolated",
                refId: lot.Id));
        }

        procurement.TraceEvents.Add(ProcurementTrace.Capture(
            product.Id,
            "receiving",
            isolated ? "isolated" : "active",
            qty,
            product.BaseUom,
            "DemoSeeder",
            "DemoSeed",
            lot.Id,
            lot.Id,
            destLocation: zone.Code));

        return 1;
    }

    private static async Task<IReadOnlyList<Store>> EnsureCustomersAndStoresAsync(
        OrderingDbContext ordering,
        Guid warehouseId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var harbor = await GetOrCreateOrgAsync(ordering, "HBRBISTRO", "Harbor Bistro Group", cancellationToken)
            .ConfigureAwait(false);
        var campus = await GetOrCreateOrgAsync(ordering, "CAMPUS", "Campus Dining Co", cancellationToken)
            .ConfigureAwait(false);
        await ordering.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var stores = new List<Store>
        {
            await GetOrCreateStoreAsync(ordering, harbor.Id, "HB-DT", "Harbor Downtown", "100 Atlantic Ave, Boston", warehouseId, cancellationToken).ConfigureAwait(false),
            await GetOrCreateStoreAsync(ordering, harbor.Id, "HB-BB", "Harbor Back Bay", "500 Boylston St, Boston", warehouseId, cancellationToken).ConfigureAwait(false),
            await GetOrCreateStoreAsync(ordering, campus.Id, "CD-MN", "Campus Main Cafe", "1 University Rd, Cambridge", warehouseId, cancellationToken).ConfigureAwait(false),
            await GetOrCreateStoreAsync(ordering, campus.Id, "CD-NK", "Campus North Kiosk", "88 Hampshire St, Cambridge", warehouseId, cancellationToken).ConfigureAwait(false),
        };
        await ordering.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "[demo-seed] [{Tenant}] 2 customer orgs × 4 stores on warehouse {Warehouse}",
                DemoSeeder.Acme.Id, WarehouseCode);
        }
        return stores;
    }

    private static async Task<CustomerOrg> GetOrCreateOrgAsync(
        OrderingDbContext ordering,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        string normalized = code.Trim().ToUpperInvariant();
        var existing = await ordering.CustomerOrgs
            .FirstOrDefaultAsync(o => o.Code == normalized, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var org = CustomerOrg.Create(normalized, name);
        ordering.CustomerOrgs.Add(org);
        return org;
    }

    private static async Task<Store> GetOrCreateStoreAsync(
        OrderingDbContext ordering,
        Guid orgId,
        string code,
        string name,
        string address,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        string normalized = code.Trim().ToUpperInvariant();
        var existing = await ordering.Stores
            .FirstOrDefaultAsync(s => s.Code == normalized, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var store = Store.Create(orgId, normalized, name, address, warehouseId, deliveryWindow: "05:00-08:00");
        ordering.Stores.Add(store);
        return store;
    }

    private static async Task EnsureContractPricesAsync(
        CatalogDbContext catalog,
        OrderingDbContext ordering,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var harbor = await ordering.CustomerOrgs
            .FirstOrDefaultAsync(o => o.Code == "HBRBISTRO", cancellationToken)
            .ConfigureAwait(false);
        var campus = await ordering.CustomerOrgs
            .FirstOrDefaultAsync(o => o.Code == "CAMPUS", cancellationToken)
            .ConfigureAwait(false);
        if (harbor is null || campus is null)
        {
            return;
        }

        var milk = await catalog.Products
            .FirstOrDefaultAsync(p => p.Sku == "HD-ML-501", cancellationToken)
            .ConfigureAwait(false);
        var romaine = await catalog.Products
            .FirstOrDefaultAsync(p => p.Sku == "FL-LT-401", cancellationToken)
            .ConfigureAwait(false);
        if (milk is null || romaine is null)
        {
            return;
        }

        int added = 0;
        added += await AddPriceListIfMissingAsync(
            catalog,
            "Harbor Bistro contract",
            harbor.Id,
            [(milk.Id, 4.19m), (romaine.Id, 7.60m)],
            cancellationToken).ConfigureAwait(false);
        added += await AddPriceListIfMissingAsync(
            catalog,
            "Campus Dining contract",
            campus.Id,
            [(milk.Id, 4.89m), (romaine.Id, 8.10m)],
            cancellationToken).ConfigureAwait(false);

        if (added == 0)
        {
            return;
        }

        await catalog.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[demo-seed] [{Tenant}] seeded 2 customer contract price lists", DemoSeeder.Acme.Id);
        }
    }

    private static async Task<int> AddPriceListIfMissingAsync(
        CatalogDbContext catalog,
        string name,
        Guid customerOrgId,
        IReadOnlyList<(Guid ProductId, decimal UnitPrice)> lines,
        CancellationToken cancellationToken)
    {
        bool exists = await catalog.PriceLists
            .AnyAsync(p => p.Name == name, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return 0;
        }

        var list = PriceList.Create(name, customerOrgId, DateTimeOffset.UtcNow.AddDays(-1), validTo: null, priority: 10);
        foreach (var (productId, unitPrice) in lines)
        {
            list.UpsertLine(productId, 1m, unitPrice, "USD");
        }

        catalog.PriceLists.Add(list);
        return 1;
    }

    private static async Task EnsureLogisticsAsync(
        LogisticsDbContext logistics,
        UserManager<FshUser> users,
        Guid warehouseId,
        IReadOnlyList<Store> stores,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var vehicle = await logistics.Vehicles
            .FirstOrDefaultAsync(v => v.Plate == VehiclePlate, cancellationToken)
            .ConfigureAwait(false);
        if (vehicle is null)
        {
            vehicle = LogisticsVehicle.Create(VehiclePlate, "Ambient,Chilled,Frozen", 3500m);
            logistics.Vehicles.Add(vehicle);
        }

        var driverUser = await users.FindByEmailAsync(DriverEmail).ConfigureAwait(false);
        if (driverUser is not null && Guid.TryParse(driverUser.Id, out var userId))
        {
            bool driverExists = await logistics.Drivers
                .AnyAsync(d => d.UserId == userId, cancellationToken)
                .ConfigureAwait(false);
            if (!driverExists)
            {
                logistics.Drivers.Add(LogisticsDriver.Create(userId, "+16175550109"));
            }
        }
        else
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "[demo-seed] [{Tenant}] {Email} not found — skip Driver row",
                    DemoSeeder.Acme.Id, DriverEmail);
            }
        }

        bool routeExists = await logistics.Routes
            .AnyAsync(r => r.WarehouseId == warehouseId && r.Code == RouteCode, cancellationToken)
            .ConfigureAwait(false);
        if (!routeExists)
        {
            var storeIds = stores.Select(s => s.Id).ToList();
            logistics.Routes.Add(LogisticsRoute.Create(warehouseId, RouteCode, storeIds, vehicle.Id));
        }

        await logistics.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "[demo-seed] [{Tenant}] vehicle {Plate}, route {Route}, driver {Email}",
                DemoSeeder.Acme.Id, VehiclePlate, RouteCode, DriverEmail);
        }
    }

    private static TemperatureZoneKind ToKind(CatalogTemperature zone)
        => zone switch
        {
            CatalogTemperature.Ambient => TemperatureZoneKind.Ambient,
            CatalogTemperature.Chilled => TemperatureZoneKind.Chilled,
            CatalogTemperature.Frozen => TemperatureZoneKind.Frozen,
            _ => TemperatureZoneKind.Ambient
        };

    private static string LocationPrefix(TemperatureZoneKind kind)
        => kind switch
        {
            TemperatureZoneKind.Ambient => "AMB",
            TemperatureZoneKind.Chilled => "CHL",
            TemperatureZoneKind.Frozen => "FRZ",
            _ => "ZON"
        };
}
