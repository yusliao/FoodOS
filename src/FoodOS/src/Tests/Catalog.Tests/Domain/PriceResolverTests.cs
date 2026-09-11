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

    private static (decimal UnitPrice, string Currency, string Source) Quote(
        decimal quantity,
        ProductContractLock? priceLock = null,
        params PriceList[] lists)
        => PriceResolver.Resolve(OrgA, ProductId, CatalogPrice, quantity, AsOf, priceLock, lists);

    private static PriceList Contract(Guid orgId, decimal unitPrice, int priority = 1)
    {
        var list = PriceList.Create("contract", orgId, AsOf.AddDays(-1), AsOf.AddYears(1), priority);
        list.UpsertLine(ProductId, 1m, unitPrice, "USD");
        return list;
    }
}
