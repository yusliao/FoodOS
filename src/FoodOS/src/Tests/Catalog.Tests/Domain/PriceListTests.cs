using FSH.Modules.Catalog.Domain;

namespace Catalog.Tests.Domain;

public sealed class PriceListTests
{
    [Fact]
    public void Create_Should_TreatEmptyCustomerOrgAsCatalogWide()
    {
        var list = PriceList.Create("catalog", Guid.Empty, DateTimeOffset.UtcNow);

        list.CustomerOrgId.ShouldBeNull();
        list.Name.ShouldBe("catalog");
    }

    [Fact]
    public void IsValidAt_Should_BeInclusiveOfBounds()
    {
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
        var list = PriceList.Create("window", Guid.CreateVersion7(), from, to);

        list.IsValidAt(from).ShouldBeTrue();
        list.IsValidAt(to).ShouldBeTrue();
        list.IsValidAt(to.AddSeconds(1)).ShouldBeFalse();
        list.IsValidAt(from.AddSeconds(-1)).ShouldBeFalse();
    }

    [Fact]
    public void UpsertLine_Should_ReplacePriceForSameProductAndMinQty()
    {
        var list = PriceList.Create("A", Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        var productId = Guid.CreateVersion7();
        PriceListLine first = list.UpsertLine(productId, 1m, 10m, "usd");
        PriceListLine second = list.UpsertLine(productId, 1m, 8m, "USD");

        second.Id.ShouldBe(first.Id);
        list.Lines.Count.ShouldBe(1);
        list.Lines[0].UnitPrice.ShouldBe(8m);
        list.Lines[0].Currency.ShouldBe("USD");
    }

    [Fact]
    public void Create_Should_Throw_When_ValidToIsBeforeValidFrom()
    {
        var from = DateTimeOffset.UtcNow;
        Should.Throw<ArgumentException>(() =>
            PriceList.Create("bad", Guid.CreateVersion7(), from, from.AddDays(-1)));
    }
}
