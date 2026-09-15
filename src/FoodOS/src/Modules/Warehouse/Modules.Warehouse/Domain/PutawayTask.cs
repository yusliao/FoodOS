using System.Net;
using FSH.Framework.Core.Domain;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Warehouse.Domain;

public sealed class PutawayTask : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public Guid WarehouseId { get; private set; }
    public Guid ZoneId { get; private set; }
    public string Zone { get; private set; } = default!;
    public Guid ProductId { get; private set; }
    public Guid LotId { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid? SuggestedLocationId { get; private set; }
    public Guid? LocationId { get; private set; }
    public string Source { get; private set; } = default!;
    public Guid? RefId { get; private set; }
    public PutawayTaskStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PutawayTask() { }

    public static PutawayTask Create(
        Guid warehouseId,
        Guid zoneId,
        string zone,
        Guid productId,
        Guid lotId,
        decimal quantity,
        string source,
        Guid? suggestedLocationId,
        Guid? refId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zone);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new PutawayTask
        {
            Id = Guid.CreateVersion7(),
            WarehouseId = warehouseId,
            ZoneId = zoneId,
            Zone = zone.Trim(),
            ProductId = productId,
            LotId = lotId,
            Quantity = quantity,
            SuggestedLocationId = suggestedLocationId,
            Source = source.Trim(),
            RefId = refId,
            Status = PutawayTaskStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Complete(Guid locationId)
    {
        if (Status == PutawayTaskStatus.Completed)
        {
            return;
        }

        if (Status == PutawayTaskStatus.Cancelled)
        {
            throw new CustomException(
                "Cancelled putaway tasks cannot be confirmed.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        if (locationId == Guid.Empty)
        {
            throw new CustomException(
                "Location is required to confirm putaway.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        LocationId = locationId;
        Status = PutawayTaskStatus.Completed;
    }

    public void Cancel()
    {
        if (Status == PutawayTaskStatus.Completed)
        {
            throw new CustomException(
                "Completed putaway tasks cannot be cancelled.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        Status = PutawayTaskStatus.Cancelled;
    }
}
