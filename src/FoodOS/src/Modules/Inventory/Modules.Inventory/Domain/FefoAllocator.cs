namespace FSH.Modules.Inventory.Domain;

/// <summary>
/// FEFO selection for wave allocation. Isolated / non-active lots are skipped.
/// </summary>
public static class FefoAllocator
{
    public readonly record struct Candidate(Lot Lot, LotBalance Balance);

    public readonly record struct Slice(Lot Lot, LotBalance Balance, decimal Quantity);

    public static IReadOnlyList<Slice> Take(
        IEnumerable<Candidate> candidates,
        decimal quantity,
        DateOnly today,
        int minRemainingDaysOnShip = 0)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        var eligible = candidates
            .Where(c => c.Lot.Status == LotStatus.Active)
            .Where(c => c.Lot.ExpiryDate.DayNumber - today.DayNumber >= minRemainingDaysOnShip)
            .Where(c => c.Balance.Available > 0)
            .OrderBy(c => c.Lot.ExpiryDate)
            .ThenBy(c => c.Lot.CreatedAtUtc)
            .ThenBy(c => c.Lot.Id)
            .ToList();

        var result = new List<Slice>();
        decimal remaining = quantity;
        foreach (var candidate in eligible)
        {
            if (remaining <= 0)
            {
                break;
            }

            decimal take = Math.Min(candidate.Balance.Available, remaining);
            if (take <= 0)
            {
                continue;
            }

            result.Add(new Slice(candidate.Lot, candidate.Balance, take));
            remaining -= take;
        }

        return result;
    }
}
