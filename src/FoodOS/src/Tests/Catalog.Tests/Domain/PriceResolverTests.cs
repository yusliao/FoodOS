using FSH.Modules.Catalog.Domain;

namespace Catalog.Tests.Domain;

public sealed class PriceResolverTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ProductId = Guid.CreateVersion7();
    private static readonly Guid OrgA = Guid.CreateVersion7();
    private static readonly Guid OrgB = Guid.CreateVersion7();
    private static readonly Money CatalogPrice = new(20m, "USD");

    [Fact]
    public void Resolve_Should_UseCatalogPrice_When_NoLockOrContract()
    {
        var (price, currency, source) = Quote(5m);

        price.ShouldBe(20m);
        currency.ShouldBe("USD");
        source.ShouldBe(PriceQuoteSource.Catalog);
    }

    [Fact]
    public void Resolve_Should_PreferLock_OverContractAndCatalog()
    {
        var list = Contract(OrgA, 8m);
        var priceLock = ProductContractLock.Create(OrgA, ProductId, 6m, "USD", AsOf.AddDays(30));

        var (price, _, source) = Quote(5m, priceLock, list);

        price.ShouldBe(6m);
        source.ShouldBe(PriceQuoteSource.Locked);
    }

    [Fact]
    public void Resolve_Should_IgnoreExpiredLock()
    {
        var list = Contract(OrgA, 8m);
        var priceLock = ProductContractLock.Create(OrgA, ProductId, 6m, "USD", AsOf.AddDays(-1));

        var (price, _, source) = Quote(5m, priceLock, list);

        price.ShouldBe(8m);
        source.ShouldBe(PriceQuoteSource.Contract);
    }

    [Fact]
    public void Resolve_Should_MatchHighestMinQtyNotExceedingQuantity()
    {
        var list = PriceList.Create("A-tier", OrgA, AsOf.AddDays(-1), AsOf.AddYears(1), priority: 1);
        list.UpsertLine(ProductId, 1m, 10m, "USD");
        list.UpsertLine(ProductId, 10m, 7m, "USD");

        var (atFive, _, _) = Quote(5m, null, list);
        var (atTen, _, source) = Quote(10m, null, list);

        atFive.ShouldBe(10m);
        atTen.ShouldBe(7m);
        source.ShouldBe(PriceQuoteSource.Contract);
    }

    [Fact]
    public void Resolve_Should_PreferHigherPriorityCustomerList()
    {
        var low = Contract(OrgA, 9m, priority: 1);
        var high = Contract(OrgA, 5m, priority: 5);

        var (price, _, _) = Quote(1m, null, low, high);

        price.ShouldBe(5m);
    }

    [Fact]
    public void Resolve_Should_IgnoreOtherCustomersListsAndLocks()
    {
        var listB = Contract(OrgB, 3m);
        var lockB = ProductContractLock.Create(OrgB, ProductId, 1m, "USD", AsOf.AddDays(30));

        var (price, _, source) = Quote(1m, lockB, listB);

        price.ShouldBe(20m);
        source.ShouldBe(PriceQuoteSource.Catalog);
    }

    [Fact]
    public void Resolve_Should_UseCatalogWideList_When_NoCustomerContract()
    {
        var catalogList = PriceList.Create("promo", customerOrgId: null, AsOf.AddDays(-1), AsOf.AddYears(1), 1);
        catalogList.UpsertLine(ProductId, 1m, 15m, "USD");

        var (price, _, source) = Quote(2m, null, catalogList);

        price.ShouldBe(15m);
        source.ShouldBe(PriceQuoteSource.Catalog);
    }

    [Fact]
    public void Resolve_Should_PreferCustomerContract_OverCatalogWideList()
    {
        var catalogList = PriceList.Create("promo", customerOrgId: null, AsOf.AddDays(-1), AsOf.AddYears(1), 99);
        catalogList.UpsertLine(ProductId, 1m, 15m, "USD");
        var contract = Contract(OrgA, 8m, priority: 0);

        var (price, _, source) = Quote(1m, null, catalogList, contract);

        price.ShouldBe(8m);
        source.ShouldBe(PriceQuoteSource.Contract);
    }

    [Fact]
    public void Resolve_Should_IgnoreExpiredPriceList()
    {
        var expired = PriceList.Create("old", OrgA, AsOf.AddYears(-2), AsOf.AddDays(-1), 10);
        expired.UpsertLine(ProductId, 1m, 1m, "USD");

        var (price, _, source) = Quote(1m, null, expired);

        price.ShouldBe(20m);
        source.ShouldBe(PriceQuoteSource.Catalog);
    }

    [Fact]
    public void Resolve_Should_IgnoreLinesForOtherProducts()
    {
        var other = Guid.CreateVersion7();
        var list = PriceList.Create("A", OrgA, AsOf.AddDays(-1), AsOf.AddYears(1), 1);
        list.UpsertLine(other, 1m, 1m, "USD");

        var (price, _, source) = Quote(1m, null, list);

        price.ShouldBe(20m);
        source.ShouldBe(PriceQuoteSource.Catalog);
    }

    [Fact]
    public void Resolve_Should_IgnoreLockForDifferentProduct()
    {
        var priceLock = ProductContractLock.Create(OrgA, Guid.CreateVersion7(), 1m, "USD", AsOf.AddDays(1));

        var (price, _, source) = Quote(1m, priceLock);

        price.ShouldBe(20m);
        source.ShouldBe(PriceQuoteSource.Catalog);
    }

    [Fact]
    public void Resolve_Should_ApplyLevelDiscount_ToCatalogPrice()
    {
        var vip = CustomerLevel.Create("vip", "VIP", 10m);

        var (price, _, source) = QuoteAtLevel(vip, 1m);

        price.ShouldBe(18m);
        source.ShouldBe(PriceQuoteSource.Level);
    }

    [Fact]
    public void Resolve_Should_ApplyLevelDiscount_ToCatalogWideListPrice()
    {
        var vip = CustomerLevel.Create("VIP", "VIP", 10m);
        var promo = PriceList.Create("promo", customerOrgId: null, AsOf.AddDays(-1), AsOf.AddYears(1), 1);
        promo.UpsertLine(ProductId, 1m, 15m, "USD");

        var (price, _, source) = QuoteAtLevel(vip, 1m, promo);

        price.ShouldBe(13.5m);
        source.ShouldBe(PriceQuoteSource.Level);
    }

    [Fact]
    public void Resolve_Should_PreferLevelListPrice_OverLevelDiscount()
    {
        var vip = CustomerLevel.Create("VIP", "VIP", 10m);
        var levelList = LevelList(vip, 12m);

        var (price, _, source) = QuoteAtLevel(vip, 1m, levelList);

        price.ShouldBe(12m);
        source.ShouldBe(PriceQuoteSource.Level);
    }

    [Fact]
    public void Resolve_Should_PreferCustomerContract_OverLevelPrice()
    {
        var vip = CustomerLevel.Create("VIP", "VIP", 10m);
        var levelList = LevelList(vip, 12m);
        var contract = Contract(OrgA, 8m, priority: 0);

        var (price, _, source) = QuoteAtLevel(vip, 1m, levelList, contract);

        price.ShouldBe(8m);
        source.ShouldBe(PriceQuoteSource.Contract);
    }

    [Fact]
    public void Resolve_Should_IgnoreOtherLevelsLists_And_NotTreatThemAsCatalogWide()
    {
        var vip = CustomerLevel.Create("VIP", "VIP", 0m);
        var gold = CustomerLevel.Create("GOLD", "Gold", 0m);
        var goldList = LevelList(gold, 5m);

        var (asVip, _, vipSource) = QuoteAtLevel(vip, 1m, goldList);
        var (noLevel, _, noLevelSource) = Quote(1m, null, goldList);

        asVip.ShouldBe(20m);
        vipSource.ShouldBe(PriceQuoteSource.Catalog);
        noLevel.ShouldBe(20m);
        noLevelSource.ShouldBe(PriceQuoteSource.Catalog);
    }

    [Fact]
    public void ListPrice_Should_UseCatalogWideTier_And_IgnoreCustomerAndLevelLists()
    {
        var vip = CustomerLevel.Create("VIP", "VIP", 10m);
        var promo = PriceList.Create("promo", customerOrgId: null, AsOf.AddDays(-1), AsOf.AddYears(1), 1);
        promo.UpsertLine(ProductId, 1m, 15m, "USD");

        var (withPromo, _) = PriceResolver.ListPrice(ProductId, CatalogPrice, 1m, AsOf, [promo, LevelList(vip, 5m), Contract(OrgA, 4m)]);
        var (noLists, _) = PriceResolver.ListPrice(ProductId, CatalogPrice, 1m, AsOf, [LevelList(vip, 5m)]);

        withPromo.ShouldBe(15m);
        noLists.ShouldBe(20m);
    }

    [Fact]
    public void Resolve_Should_RoundLevelDiscount_ToCents()
    {
        var level = CustomerLevel.Create("B", "B", 12.5m);

        var (price, _, _) = PriceResolver.Resolve(
            OrgA, ProductId, new Money(9.99m, "USD"), 1m, AsOf, null, [], level);

        price.ShouldBe(8.74m);
    }

    private static (decimal UnitPrice, string Currency, string Source) Quote(
        decimal quantity,
        ProductContractLock? priceLock = null,
        params PriceList[] lists)
        => PriceResolver.Resolve(OrgA, ProductId, CatalogPrice, quantity, AsOf, priceLock, lists);

    private static (decimal UnitPrice, string Currency, string Source) QuoteAtLevel(
        CustomerLevel level,
        decimal quantity,
        params PriceList[] lists)
        => PriceResolver.Resolve(OrgA, ProductId, CatalogPrice, quantity, AsOf, null, lists, level);

    private static PriceList LevelList(CustomerLevel level, decimal unitPrice)
    {
        var list = PriceList.Create("level", customerOrgId: null, AsOf.AddDays(-1), AsOf.AddYears(1), 1, level.Id);
        list.UpsertLine(ProductId, 1m, unitPrice, "USD");
        return list;
    }

    private static PriceList Contract(Guid orgId, decimal unitPrice, int priority = 1)
    {
        var list = PriceList.Create("contract", orgId, AsOf.AddDays(-1), AsOf.AddYears(1), priority);
        list.UpsertLine(ProductId, 1m, unitPrice, "USD");
        return list;
    }
}
