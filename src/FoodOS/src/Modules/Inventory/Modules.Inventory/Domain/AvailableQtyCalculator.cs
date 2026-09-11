namespace FSH.Modules.Inventory.Domain;

public static class AvailableQtyCalculator
{
    public static decimal Compute(IEnumerable<LotBalance> balances, IReadOnlyDictionary<Guid, Lot> lots, DateOnly today, int minRemainingDaysOnShip = 0)
    {
        ArgumentNullException.ThrowIfNull(balances);
        ArgumentNullException.ThrowIfNull(lots);

        decimal total = 0m;
        foreach (var balance in balances)
        {
            if (!lots.TryGetValue(balance.LotId, out var lot))
            {
                continue;
            }

            if (lot.Status != LotStatus.Active)
            {
                continue;
            }

            int remaining = lot.ExpiryDate.DayNumber - today.DayNumber;
            if (remaining < minRemainingDaysOnShip)
            {
                continue;
            }

            decimal available = balance.Available;
            if (available > 0)
            {
                total += available;
            }
        }

        return total;
    }
}
