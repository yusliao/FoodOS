using FSH.Framework.Core.Domain;

namespace FSH.Modules.Warehouse.Domain;

public sealed class Shrinkage : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public Guid WarehouseId { get; private set; }
    public string Zone { get; private set; } = default!;
    public Guid ProductId { get; private set; }
    public Guid LotId { get; private set; }
    public decimal Quantity { get; private set; }
    public string Reason { get; private set; } = default!;
    public string PhotoFileIds { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Shrinkage() { }

    public static Shrinkage Create(
        Guid warehouseId,
        string zone,
        Guid productId,
        Guid lotId,
        decimal quantity,
        string reason,
        Guid actorUserId,
        IReadOnlyList<Guid>? photoFileIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zone);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new Shrinkage
        {
            Id = Guid.CreateVersion7(),
            WarehouseId = warehouseId,
            Zone = zone.Trim(),
            ProductId = productId,
            LotId = lotId,
            Quantity = quantity,
            Reason = reason.Trim(),
            PhotoFileIds = photoFileIds is { Count: > 0 }
                ? string.Join(',', photoFileIds)
                : string.Empty,
            ActorUserId = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public IReadOnlyList<Guid> PhotoIds()
    {
        if (string.IsNullOrWhiteSpace(PhotoFileIds))
        {
            return [];
        }

        return PhotoFileIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Guid.Parse)
            .ToList();
    }
}
