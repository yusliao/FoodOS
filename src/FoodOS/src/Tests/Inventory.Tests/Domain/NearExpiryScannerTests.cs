using FSH.Modules.Inventory.Domain;

namespace Inventory.Tests.Domain;

public sealed class NearExpiryScannerTests
{
    [Fact]
    public void Scan_Should_IncludeLotsWithinLeadWindow_And_SkipIsolated()
    {
        var productId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var zoneId = Guid.CreateVersion7();
        DateOnly asOf = new(2026, 9, 13);

        var near = Lot.Create("NEAR", productId, asOf.AddDays(2));
        var far = Lot.Create("FAR", productId, asOf.AddDays(10));
        var isolated = Lot.Create("ISO", productId, asOf.AddDays(1));
        isolated.Isolate();
        var expired = Lot.Create("OLD", productId, asOf.AddDays(-1));

        var nearBal = LotBalance.Create(warehouseId, zoneId, near.Id, productId);
        nearBal.Receive(6);
        var farBal = LotBalance.Create(warehouseId, zoneId, far.Id, productId);
        farBal.Receive(9);
        var isoBal = LotBalance.Create(warehouseId, zoneId, isolated.Id, productId);
        isoBal.Receive(4);
        var oldBal = LotBalance.Create(warehouseId, zoneId, expired.Id, productId);
        oldBal.Receive(3);

        var hits = NearExpiryScanner.Scan(
            [near, far, isolated, expired],
            [nearBal, farBal, isoBal, oldBal],
            asOf,
            leadDays: 3);

        hits.Count.ShouldBe(2);
        hits.ShouldContain(h => h.LotId == near.Id && h.AtRiskQty == 6m);
        hits.ShouldContain(h => h.LotId == expired.Id && h.AtRiskQty == 3m);
        hits.ShouldNotContain(h => h.LotId == far.Id);
        hits.ShouldNotContain(h => h.LotId == isolated.Id);
    }
}
