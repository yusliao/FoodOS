using FSH.Framework.Core.Domain;

namespace FSH.Modules.Warehouse.Domain;

public sealed class StockPlacement : BaseEntity<Guid>
{
    public Guid LotId { get; private set; }
    public Guid LocationId { get; private set; }
    public decimal Quantity { get; private set; }

    private StockPlacement() { }

    public static StockPlacement Create(Guid lotId, Guid locationId, decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new StockPlacement
        {
            Id = Guid.CreateVersion7(),
            LotId = lotId,
            LocationId = locationId,
            Quantity = quantity
        };
    }

    public void Add(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        Quantity += quantity;
    }
}
