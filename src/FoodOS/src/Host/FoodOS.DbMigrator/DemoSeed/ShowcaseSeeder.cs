using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Identity.Claims;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using FSH.Modules.Identity.Data;
using FSH.Modules.Identity.Domain;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Tickets.Contracts.Dtos;
using FSH.Modules.Tickets.Data;
using FSH.Modules.Tickets.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodOS.DbMigrator.DemoSeed;

/// <summary>
/// An opt-in, additive restaurant showcase for an EXISTING customer tenant.
/// Unlike seed-demo, this never changes passwords, creates live stock, or writes invoices.
/// </summary>
internal sealed class ShowcaseSeeder(IServiceProvider services)
{
    private const string OrgCode = "DEMO-REST";
    private const string StoreCode = "DEMO-BISTRO";
    private const string WarehouseCode = "DEMO-DC";
    private const string SupplierCode = "DEMO-SUP";
    private const string PriceListName = "DEMO Restaurant Contract";
    private const string TicketNumber = "DEMO-SHOWCASE-001";

    private static readonly CatalogSeedData.ProductSpec[] FoodProducts =
        [.. CatalogSeedData.ProductSpecs.Where(product => product.Stock == 0)];

    private static readonly Dictionary<string, string> ChineseProductNames = new(StringComparer.Ordinal)
    {
        ["FL-LT-401"] = "罗马生菜心 6棵/箱",
        ["FL-SP-402"] = "嫩菠菜 2.5磅/盒",
        ["FL-TM-403"] = "藤蔓番茄 5磅/箱",
        ["FL-ST-404"] = "草莓 1磅/盒",
        ["FL-CU-405"] = "黄瓜 12根/箱",
        ["HD-ML-501"] = "全脂牛奶 1加仑",
        ["HD-YG-502"] = "原味希腊酸奶 32盎司",
        ["HD-BT-503"] = "无盐黄油 1磅",
        ["HD-CH-504"] = "切达奶酪 5磅",
        ["HD-EG-505"] = "大号鸡蛋 18枚",
        ["AP-PS-601"] = "急冻青豆 2.5磅",
        ["AP-BR-602"] = "急冻混合莓果 2磅",
        ["AP-CK-603"] = "急冻鸡胸肉 10磅",
        ["AP-FR-604"] = "急冻薯条 5磅",
        ["AP-IC-605"] = "香草冰淇淋 3加仑",
        ["AP-PZ-606"] = "奶酪披萨 12英寸×8",
        ["PC-RC-701"] = "茉莉香米 25磅",
        ["PC-OL-702"] = "特级初榨橄榄油 3升",
        ["PC-CN-703"] = "碎番茄罐头 6罐",
        ["PC-PN-704"] = "通心粉 10磅",
        ["PC-FL-705"] = "通用面粉 25磅",
        ["PC-SG-706"] = "蔗糖 25磅",
    };

    private static readonly (string Email, string UserName, string FirstName, string LastName, string Role)[] OperatorUsers =
    [
        ("demo.manager@invalid.example", "demo.operator.manager", "Demo", "Manager", "Manager"),
        ("demo.support@invalid.example", "demo.operator.support", "Demo", "Support", "Support"),
        ("demo.purchaser@invalid.example", "demo.operator.purchaser", "Demo", "Purchaser", "Purchaser"),
    ];

    public async Task RunAsync(string tenantId, bool apply, CancellationToken ct)
    {
        using var catalogScope = services.CreateScope();
        var tenantStore = catalogScope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var root = await tenantStore.GetAsync(MultitenancyConstants.Root.Id).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The root tenant is missing. Run the normal migrations first.");
        var customer = await tenantStore.GetAsync(tenantId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Customer tenant '{tenantId}' does not exist. Create it in admin first.");
        if (string.IsNullOrWhiteSpace(customer.AdminEmail))
        {
            throw new InvalidOperationException($"Customer tenant '{tenantId}' has no administrator email.");
        }

        await using var rootScope = services.CreateAsyncScope();
        rootScope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(root);
        var catalog = rootScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var ordering = rootScope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var inventory = rootScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var procurement = rootScope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var identity = rootScope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await using var customerScope = services.CreateAsyncScope();
        customerScope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(customer);
        var customerUsers = customerScope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var customerAdmin = await customerUsers.FindByEmailAsync(customer.AdminEmail).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant '{tenantId}' administrator is missing. Complete its normal provisioning first.");
        if (!Guid.TryParse(customerAdmin.Id, out var customerAdminId))
        {
            throw new InvalidOperationException("The customer administrator ID is not a GUID.");
        }

        // Preview is intentionally read-only. No migrations, root seeding, account creation,
        // or password changes are performed by this command unless --apply-showcase is present.
        var existingProducts = await catalog.Products
            .CountAsync(product => product.Sku.StartsWith("DEMO-"), ct).ConfigureAwait(false);
        var existingOrg = await ordering.CustomerOrgs
            .FirstOrDefaultAsync(org => org.Code == OrgCode, ct).ConfigureAwait(false);
        var normalizedTenantId = tenantId.ToUpperInvariant();
        var tenantOrg = await ordering.CustomerOrgs
            .FirstOrDefaultAsync(org => org.CustomerTenantId == normalizedTenantId, ct)
            .ConfigureAwait(false);
        if (tenantOrg is not null && tenantOrg.Code != OrgCode)
        {
            throw new InvalidOperationException(
                $"Tenant '{tenantId}' is already mapped to customer '{tenantOrg.Code}'. " +
                "Use a fresh showcase tenant; no records were written.");
        }
        if (existingOrg is not null &&
            !string.Equals(existingOrg.CustomerTenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Customer code {OrgCode} already belongs to tenant '{existingOrg.CustomerTenantId}'.");
        }
        var existingStore = await ordering.Stores
            .FirstOrDefaultAsync(store => store.Code == StoreCode, ct).ConfigureAwait(false);
        if (existingStore is not null &&
            !string.Equals(existingStore.CustomerTenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Store code {StoreCode} already belongs to tenant '{existingStore.CustomerTenantId}'.");
        }
        if (existingOrg is not null && existingStore is not null && existingStore.CustomerOrgId != existingOrg.Id)
        {
            throw new InvalidOperationException($"Store {StoreCode} is associated with another customer organization.");
        }
        var existingPriceList = await catalog.PriceLists
            .FirstOrDefaultAsync(priceList => priceList.Name == PriceListName, ct).ConfigureAwait(false);
        if (existingPriceList is not null &&
            (existingOrg is null || existingPriceList.CustomerOrgId != existingOrg.Id))
        {
            throw new InvalidOperationException($"Price list {PriceListName} belongs to another customer organization.");
        }

        await Console.Out.WriteLineAsync(
            $"[showcase] tenant={customer.Id}; mode={(apply ? "APPLY" : "PREVIEW")}; " +
            $"food SKUs={FoodProducts.Length}, existing DEMO SKUs={existingProducts}; " +
            $"customer={(existingOrg is null ? "create" : "keep")}; store={(existingStore is null ? "create" : "keep")}")
            .ConfigureAwait(false);
        await Console.Out.WriteLineAsync(
            "[showcase] scope: restaurant catalog, 3 inactive operator examples, 1 supplier, " +
            "1 empty warehouse, customer/store access, contract prices, 1 customer ticket; " +
            "no orders, balances, stock movements, invoices, WMS callbacks, or password resets.")
            .ConfigureAwait(false);
        if (!apply)
        {
            await Console.Out.WriteLineAsync("[showcase] preview only; pass --apply-showcase after backup to insert missing records.")
                .ConfigureAwait(false);
            return;
        }

        await EnsureOperatorRolesAndUsersAsync(rootScope.ServiceProvider, identity, ct).ConfigureAwait(false);
        await EnsureCatalogAsync(catalog, ct).ConfigureAwait(false);
        await EnsureSupplierAsync(procurement, ct).ConfigureAwait(false);
        var warehouse = await EnsureWarehouseAsync(inventory, ct).ConfigureAwait(false);
        var org = await EnsureCustomerAsync(ordering, existingOrg, tenantId, ct).ConfigureAwait(false);
        var store = await EnsureStoreAsync(ordering, existingStore, org.Id, warehouse.Id, tenantId, ct).ConfigureAwait(false);
        await EnsureContractPricesAsync(catalog, org.Id, ct).ConfigureAwait(false);
        await EnsureCustomerAccessAsync(ordering, store, tenantId, customerAdminId, ct).ConfigureAwait(false);
        await EnsureCustomerStaffAsync(customerUsers).ConfigureAwait(false);
        await EnsureCustomerTicketAsync(customerScope.ServiceProvider, tenantId, customerAdminId, ct).ConfigureAwait(false);
        await Console.Out.WriteLineAsync("[showcase] complete; re-running keeps existing values and passwords unchanged.")
            .ConfigureAwait(false);
    }

    private static async Task EnsureOperatorRolesAndUsersAsync(IServiceProvider provider, IdentityDbContext identity, CancellationToken ct)
    {
        var roles = provider.GetRequiredService<RoleManager<FshRole>>();
        var users = provider.GetRequiredService<UserManager<FshUser>>();
        foreach (var spec in DemoSeeder.BuildOperatorRoles().Where(role =>
                     role.Name is "Manager" or "Support" or "Purchaser"))
        {
            if (await roles.FindByNameAsync(spec.Name).ConfigureAwait(false) is not null)
            {
                continue; // Never overwrite an operator-maintained role or permission set.
            }
            var role = new FshRole(spec.Name, spec.Description, RoleAudiences.Operator);
            var result = await roles.CreateAsync(role).ConfigureAwait(false);
            EnsureSucceeded(result, $"create role '{spec.Name}'");
            foreach (var permission in spec.Permissions.Distinct(StringComparer.Ordinal))
            {
                identity.RoleClaims.Add(new FshRoleClaim
                {
                    RoleId = role.Id,
                    ClaimType = ClaimConstants.Permission,
                    ClaimValue = permission,
                    CreatedBy = "ShowcaseSeeder",
                    CreatedOn = DateTimeOffset.UtcNow,
                });
            }
            await identity.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        foreach (var spec in OperatorUsers)
        {
            if (await users.FindByEmailAsync(spec.Email).ConfigureAwait(false) is not null)
            {
                continue; // Never change an existing person's account, roles, or password.
            }
            var user = new FshUser
            {
                UserName = spec.UserName,
                Email = spec.Email,
                FirstName = spec.FirstName,
                LastName = spec.LastName,
                IsActive = false,
                EmailConfirmed = false,
            };
            EnsureSucceeded(await users.CreateAsync(user).ConfigureAwait(false), $"create inactive user '{spec.Email}'");
            EnsureSucceeded(await users.AddToRoleAsync(user, spec.Role).ConfigureAwait(false),
                $"assign role '{spec.Role}' to '{spec.Email}'");
        }
    }

    private static async Task EnsureCatalogAsync(CatalogDbContext catalog, CancellationToken ct)
    {
        var brandNames = FoodProducts.Select(product => product.BrandName).Distinct(StringComparer.Ordinal).ToList();
        foreach (var brandName in brandNames)
        {
            var name = "DEMO " + brandName;
            if (!await catalog.Brands.AnyAsync(brand => brand.Name == name, ct).ConfigureAwait(false))
            {
                catalog.Brands.Add(Brand.Create(name, "Showcase foodservice supplier brand.", null));
            }
        }
        await catalog.SaveChangesAsync(ct).ConfigureAwait(false);

        var categoryNames = FoodProducts.Select(product => product.CategoryName).Distinct(StringComparer.Ordinal).ToList();
        foreach (var categoryName in categoryNames)
        {
            var name = "DEMO " + categoryName;
            if (!await catalog.Categories.AnyAsync(category => category.Name == name, ct).ConfigureAwait(false))
            {
                catalog.Categories.Add(Category.Create(name, "Restaurant purchasing showcase category.", null));
            }
        }
        await catalog.SaveChangesAsync(ct).ConfigureAwait(false);

        var brands = await catalog.Brands.Where(brand => brand.Name.StartsWith("DEMO "))
            .ToDictionaryAsync(brand => brand.Name, ct).ConfigureAwait(false);
        var categories = await catalog.Categories.Where(category => category.Name.StartsWith("DEMO "))
            .ToDictionaryAsync(category => category.Name, ct).ConfigureAwait(false);
        foreach (var spec in FoodProducts)
        {
            var sku = "DEMO-" + spec.Sku;
            if (await catalog.Products.AnyAsync(product => product.Sku == sku, ct).ConfigureAwait(false))
            {
                continue;
            }
            catalog.Products.Add(Product.Create(
                sku,
                "DEMO " + spec.Name,
                spec.Description + " Demonstration item; not physical inventory.",
                brands["DEMO " + spec.BrandName].Id,
                categories["DEMO " + spec.CategoryName].Id,
                new Money(spec.Price, "USD"),
                stock: 0,
                spec.TemperatureZone,
                spec.ShelfLifeDays,
                spec.MinRemainingDaysOnShip));
        }
        await catalog.SaveChangesAsync(ct).ConfigureAwait(false);

        var showcaseProducts = await catalog.Products
            .Where(product => product.Sku.StartsWith("DEMO-"))
            .Include(product => product.Translations)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var product in showcaseProducts)
        {
            if (!ChineseProductNames.TryGetValue(product.Sku["DEMO-".Length..], out var chineseName) ||
                product.Translations.Any(translation => translation.Culture == "zh-CN"))
            {
                continue;
            }
            catalog.ProductTranslations.Add(ProductTranslation.Create(
                product.Id, "zh-CN", "演示 · " + chineseName, "演示商品，非真实库存。"));
        }
        await catalog.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task EnsureSupplierAsync(ProcurementDbContext procurement, CancellationToken ct)
    {
        if (await procurement.Suppliers.AnyAsync(supplier => supplier.Code == SupplierCode, ct).ConfigureAwait(false))
        {
            return;
        }
        procurement.Suppliers.Add(Supplier.Create(SupplierCode, "DEMO Foodservice Supply", "Produce,Dairy,Frozen,Grocery", 2));
        await procurement.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task<Warehouse> EnsureWarehouseAsync(InventoryDbContext inventory, CancellationToken ct)
    {
        var existing = await inventory.Warehouses.FirstOrDefaultAsync(warehouse => warehouse.Code == WarehouseCode, ct)
            .ConfigureAwait(false);
        if (existing is not null) return existing;
        var warehouse = Warehouse.Create(WarehouseCode, "DEMO Distribution Centre (no physical stock)", "Demo City");
        inventory.Warehouses.Add(warehouse);
        await inventory.SaveChangesAsync(ct).ConfigureAwait(false);
        return warehouse;
    }

    private static async Task<CustomerOrg> EnsureCustomerAsync(
        OrderingDbContext ordering, CustomerOrg? existing, string tenantId, CancellationToken ct)
    {
        if (existing is not null) return existing;
        var customer = CustomerOrg.Create(OrgCode, "DEMO Restaurant Group", customerTenantId: tenantId);
        ordering.CustomerOrgs.Add(customer);
        await ordering.SaveChangesAsync(ct).ConfigureAwait(false);
        return customer;
    }

    private static async Task<Store> EnsureStoreAsync(
        OrderingDbContext ordering, Store? existing, Guid orgId, Guid warehouseId, string tenantId, CancellationToken ct)
    {
        if (existing is not null) return existing;
        var store = Store.Create(orgId, StoreCode, "DEMO Bistro", "100 Example Street, Demo City",
            warehouseId, deliveryWindow: "05:00-08:00", customerTenantId: tenantId);
        ordering.Stores.Add(store);
        await ordering.SaveChangesAsync(ct).ConfigureAwait(false);
        return store;
    }

    private static async Task EnsureContractPricesAsync(CatalogDbContext catalog, Guid orgId, CancellationToken ct)
    {
        if (await catalog.PriceLists.AnyAsync(priceList => priceList.Name == PriceListName, ct).ConfigureAwait(false))
        {
            return;
        }
        var skus = FoodProducts.Take(4).Select(spec => "DEMO-" + spec.Sku).ToList();
        var products = await catalog.Products.Where(product => skus.Contains(product.Sku))
            .ToListAsync(ct).ConfigureAwait(false);
        if (products.Count != skus.Count)
        {
            throw new InvalidOperationException("Showcase products are incomplete; cannot create customer prices.");
        }
        var list = PriceList.Create(PriceListName, orgId, DateTimeOffset.UtcNow.AddDays(-1), priority: 10);
        foreach (var product in products)
        {
            list.UpsertLine(product.Id, 1m, Math.Round(product.Price.Amount * 0.9m, 2), "USD");
        }
        catalog.PriceLists.Add(list);
        await catalog.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task EnsureCustomerAccessAsync(
        OrderingDbContext ordering, Store store, string tenantId, Guid userId, CancellationToken ct)
    {
        var normalizedTenantId = tenantId.ToUpperInvariant();
        if (await ordering.CustomerUserStoreAccesses.AnyAsync(access =>
                access.CustomerTenantId == normalizedTenantId && access.StoreId == store.Id && access.UserId == userId,
                ct).ConfigureAwait(false))
        {
            return;
        }
        ordering.CustomerUserStoreAccesses.Add(CustomerUserStoreAccess.Create(
            tenantId, store.CustomerOrgId, store.Id, userId, DateTimeOffset.UtcNow));
        await ordering.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task EnsureCustomerTicketAsync(
        IServiceProvider customerProvider, string tenantId, Guid reporterId, CancellationToken ct)
    {
        var tickets = customerProvider.GetRequiredService<TicketsDbContext>();
        if (await tickets.Tickets.AnyAsync(ticket =>
                ticket.CustomerTenantId == tenantId && ticket.Number == TicketNumber, ct).ConfigureAwait(false))
        {
            return;
        }
        tickets.Tickets.Add(Ticket.Create(TicketNumber,
            "DEMO: Request a new delivery window",
            "Example service request from the restaurant. No real delivery has been scheduled.",
            TicketPriority.Low, reporterId, null, tenantId));
        await tickets.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task EnsureCustomerStaffAsync(UserManager<FshUser> users)
    {
        const string email = "demo.restaurant.staff@invalid.example";
        if (await users.FindByEmailAsync(email).ConfigureAwait(false) is not null)
        {
            return;
        }
        var user = new FshUser
        {
            UserName = "demo.restaurant.staff",
            Email = email,
            FirstName = "Demo",
            LastName = "Restaurant Staff",
            IsActive = false,
            EmailConfirmed = false,
        };
        EnsureSucceeded(await users.CreateAsync(user).ConfigureAwait(false), $"create inactive user '{email}'");
        EnsureSucceeded(await users.AddToRoleAsync(user, RoleConstants.Basic).ConfigureAwait(false),
            $"assign Basic role to '{email}'");
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not {operation}: " +
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
