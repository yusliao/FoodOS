using Catalog.Tests.Import;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using FSH.Modules.Catalog.Features.v1.Products.ImportProductList;
using Integration.Tests.Infrastructure;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Tests.Catalog;

/// <summary>
/// The client's product list import: preview writes nothing, apply creates inactive zero-priced products,
/// and re-importing only refreshes names without touching what operators changed.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class ProductListImportTests(FshWebApplicationFactory factory)
{
    private static readonly string[] Header =
        ["編號_x000D_\nItem No.", " ", "名稱_x000D_\nItem Name", "數量_x000D_\nQty", "單位_x000D_\nUnit", "次數量_x000D_\nSub-Qty", "次單位_x000D_\nSub-Unit"];

    [Fact]
    public async Task Import_Should_PreviewThenCreateInactive_AndOnlyRenameOnReimport()
    {
        string suffix = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string chicken = "1141" + suffix;       // 11 段 → 禽肉，按箱订、按磅结算
        string gift = "98" + suffix;            // 送海报 → 赠品
        byte[] first = Workbook(chicken, "無骨胸 Boneless Breast", gift);

        var preview = await SendAsync(new ImportProductListCommand(first, Apply: false));
        preview.Applied.ShouldBeFalse();
        preview.Created.ShouldBe(2);
        preview.Skipped.Count.ShouldBe(2); // 备注行 + 重复编号
        (await FindAsync(chicken)).ShouldBeNull();

        var applied = await SendAsync(new ImportProductListCommand(first, Apply: true));
        applied.Created.ShouldBe(2);
        var product = (await FindAsync(chicken)).ShouldNotBeNull();
        product.IsActive.ShouldBeFalse();
        product.Price.Amount.ShouldBe(0m);
        product.BaseUom.ShouldBe("CS");
        product.CatchWeight.ShouldBeTrue();
        product.TemperatureZone.ShouldBe(TemperatureZone.Frozen);
        (await CategoryNameAsync(product.CategoryId)).ShouldBe("禽肉");
        (await CategoryNameAsync((await FindAsync(gift)).ShouldNotBeNull().CategoryId)).ShouldBe(ProductListMapper.GiftCategory);

        // 运营补了价格并上架
        await WithCatalogAsync(async db =>
        {
            var tracked = await db.Products.SingleAsync(item => item.Sku == chicken);
            tracked.ChangePrice(new Money(42.5m, "USD"));
            tracked.Update(tracked.Name, tracked.Description, tracked.BrandId, tracked.CategoryId, isActive: true);
            await db.SaveChangesAsync();
        });

        var again = await SendAsync(new ImportProductListCommand(Workbook(chicken, "無骨胸(新名) Boneless Breast", gift), Apply: true));
        again.Created.ShouldBe(0);
        again.Renamed.ShouldBe(1);
        again.Unchanged.ShouldBe(1);
        var renamed = (await FindAsync(chicken)).ShouldNotBeNull();
        renamed.Name.ShouldBe("無骨胸(新名) Boneless Breast");
        renamed.IsActive.ShouldBeTrue();
        renamed.Price.Amount.ShouldBe(42.5m);
    }

    private static byte[] Workbook(string chicken, string chickenName, string gift) => XlsxTestWorkbook.Build(
        Header,
        [0, 0, "先收款後下貨!!! PAY FIRST!!!", 5, 0, 0, 0],
        [long.Parse(chicken, System.Globalization.CultureInfo.InvariantCulture), 0, chickenName, 3, "箱", 0, "磅"],
        [gift, 0, "送海報 甜酸雞 Free Poster Sweet&Sour Chicken", 2, "CS", 0, "CS"],
        [chicken, 0, "重复的行", 1, "箱", 0, "磅"]);

    private async Task<ProductImportReport> SendAsync(ImportProductListCommand command)
    {
        await using var scope = await RootScopeAsync();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private async Task<Product?> FindAsync(string sku)
    {
        Product? result = null;
        await WithCatalogAsync(async db => result = await db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Sku == sku));
        return result;
    }

    private async Task<string?> CategoryNameAsync(Guid categoryId)
    {
        string? result = null;
        await WithCatalogAsync(async db => result = await db.Categories.Where(item => item.Id == categoryId)
            .Select(item => item.Name).SingleOrDefaultAsync());
        return result;
    }

    private async Task WithCatalogAsync(Func<CatalogDbContext, Task> action)
    {
        await using var scope = await RootScopeAsync();
        await action(scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
    }

    private async Task<AsyncServiceScope> RootScopeAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var root = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(root.ShouldNotBeNull());
        return scope;
    }
}
