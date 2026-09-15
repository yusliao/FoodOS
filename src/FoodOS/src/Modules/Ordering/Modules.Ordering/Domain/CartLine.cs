using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

public sealed class CartLine : BaseEntity<Guid>, IOperatorOwnedEntity
{
    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public string Zone { get; private set; } = default!;

    private CartLine() { }

    internal static CartLine Create(Guid cartId, Guid productId, decimal quantity, string zone)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(zone);

        return new CartLine
        {
            Id = Guid.CreateVersion7(),
            CartId = cartId,
            ProductId = productId,
            Quantity = quantity,
            Zone = zone.Trim()
        };
    }
}
