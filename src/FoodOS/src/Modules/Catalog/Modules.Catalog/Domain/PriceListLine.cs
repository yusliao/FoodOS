using FSH.Framework.Core.Domain;

namespace FSH.Modules.Catalog.Domain;

public sealed class PriceListLine : BaseEntity<Guid>
{
    public Guid PriceListId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal MinQty { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = "USD";

    private PriceListLine() { }

    internal static PriceListLine Create(
        Guid priceListId,
        Guid productId,
        decimal minQty,
        decimal unitPrice,
        string currency)
    {
        if (priceListId == Guid.Empty)
        {
            throw new ArgumentException("PriceListId is required.", nameof(priceListId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        if (minQty <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minQty), "Minimum quantity must be positive.");
        }

        var money = new Money(unitPrice, currency);
        return new PriceListLine
        {
            Id = Guid.CreateVersion7(),
            PriceListId = priceListId,
            ProductId = productId,
            MinQty = minQty,
            UnitPrice = money.Amount,
            Currency = money.Currency
        };
    }

    internal void Update(decimal unitPrice, string currency)
    {
        var money = new Money(unitPrice, currency);
        UnitPrice = money.Amount;
        Currency = money.Currency;
    }
}
