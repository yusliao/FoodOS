using FSH.Framework.Core.Domain;

namespace FSH.Modules.Catalog.Domain;

/// <summary>
/// Operator-defined customer pricing level (e.g. VIP, A, B). A level discounts the catalog price by
/// <see cref="DiscountPercent"/> and may override single products through its own price lists.
/// </summary>
public sealed class CustomerLevel : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    /// <summary>Percent off the catalog price, 0 ≤ value &lt; 100 (10 = pay 90%).</summary>
    public decimal DiscountPercent { get; private set; }

    private CustomerLevel() { }

    public static CustomerLevel Create(string code, string name, decimal discountPercent)
    {
        var level = new CustomerLevel { Id = Guid.CreateVersion7() };
        level.Update(code, name, discountPercent);
        return level;
    }

    public void Update(string code, string name, decimal discountPercent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (discountPercent is < 0 or >= 100)
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercent), "Discount must be at least 0 and below 100 percent.");
        }

        Code = NormalizeCode(code);
        Name = name.Trim();
        DiscountPercent = discountPercent;
    }

    public decimal ApplyDiscount(decimal amount)
        => DiscountPercent == 0
            ? amount
            : Math.Round(amount * (100m - DiscountPercent) / 100m, 2, MidpointRounding.AwayFromZero);

    public static string NormalizeCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return code.Trim().ToUpperInvariant();
    }
}
