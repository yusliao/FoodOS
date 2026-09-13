namespace FSH.Modules.Inventory.Domain;

public sealed record NearExpiryHit(
    Guid LotId,
    string LotNo,
    Guid ProductId,
    Guid WarehouseId,
    DateOnly ExpiryDate,
    decimal AtRiskQty);

/// <summary>
/// P0 near-expiry scan: Active lots whose remaining days are within the lead window
/// (including already-expired on-hand). Does not isolate.
/// </summary>
public static class NearExpiryScanner
{
    public const int DefaultLeadDays = 3;

    public static IReadOnlyList<NearExpiryHit> Scan(
        IEnumerable<Lot> lots,
        IEnumerable<LotBalance> balances,
        DateOnly asOf,
        int leadDays = DefaultLeadDays)
    {
        ArgumentNullException.ThrowIfNull(lots);
        ArgumentNullException.ThrowIfNull(balances);

        var lotList = lots as IReadOnlyList<Lot> ?? lots.ToList();
        var qtyByLot = new Dictionary<Guid, (Guid WarehouseId, decimal Qty)>();
        foreach (var balance in balances)
        {
            decimal atRisk = balance.OnHand - balance.Isolated;
            if (atRisk <= 0)
            {
                continue;
            }

            if (qtyByLot.TryGetValue(balance.LotId, out var existing))
            {
                qtyByLot[balance.LotId] = (existing.WarehouseId, existing.Qty + atRisk);
            }
            else
            {
                qtyByLot[balance.LotId] = (balance.WarehouseId, atRisk);
            }
        }

        var hits = new List<NearExpiryHit>();
        foreach (var lot in lotList)
        {
            if (lot.Status != LotStatus.Active)
            {
                continue;
            }

            int remaining = lot.ExpiryDate.DayNumber - asOf.DayNumber;
            if (remaining > leadDays)
            {
                continue;
            }

            if (!qtyByLot.TryGetValue(lot.Id, out var qty) || qty.Qty <= 0)
            {
                continue;
            }

            hits.Add(new NearExpiryHit(lot.Id, lot.LotNo, lot.ProductId, qty.WarehouseId, lot.ExpiryDate, qty.Qty));
        }

        return hits;
    }
}
