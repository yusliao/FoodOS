using FSH.Framework.Core.Domain;

namespace FSH.Modules.Catalog.Domain;

/// <summary>
/// Highest-priority price for one customer × SKU until <see cref="Until"/>.
/// </summary>
public sealed class ProductContractLock : AggregateRoot<Guid>
{
    public Guid CustomerOrgId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = "USD";
    public DateTimeOffset Until { get; private set; }

    private ProductContractLock() { }

    public static ProductContractLock Create(
        Guid customerOrgId,
        Guid productId,
        decimal unitPrice,
        string currency,
        DateTimeOffset until)
    {
        if (customerOrgId == Guid.Empty)
        {
            throw new ArgumentException("CustomerOrgId is required.", nameof(customerOrgId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        if (until == default)
        {
            throw new ArgumentException("Until is required.", nameof(until));
        }

        var money = new Money(unitPrice, currency);
        return new ProductContractLock
        {
            Id = Guid.CreateVersion7(),
            CustomerOrgId = customerOrgId,
            ProductId = productId,
            UnitPrice = money.Amount,
            Currency = money.Currency,
            Until = until
        };
    }

    public void Replace(decimal unitPrice, string currency, DateTimeOffset until)
    {
        if (until == default)
        {
            throw new ArgumentException("Until is required.", nameof(until));
        }

        var money = new Money(unitPrice, currency);
        UnitPrice = money.Amount;
        Currency = money.Currency;
        Until = until;
    }

    public bool IsActive(DateTimeOffset asOf) => Until >= asOf;
}
