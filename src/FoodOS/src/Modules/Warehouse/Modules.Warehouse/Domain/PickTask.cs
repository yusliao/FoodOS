using System.Net;
using FSH.Framework.Core.Domain;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Warehouse.Domain;

public sealed class PickTask : BaseEntity<Guid>, IOperatorOwnedEntity
{
    public Guid WaveId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OrderLineId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Zone { get; private set; } = default!;
    public Guid LocationId { get; private set; }
    public Guid? LotId { get; private set; }
    public string? LotNo { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal ShortageQty { get; private set; }
    public PickTaskStatus Status { get; private set; }
    public Guid? PickerUserId { get; private set; }

    private PickTask() { }

    internal static PickTask Create(
        Guid waveId,
        Guid orderId,
        Guid orderLineId,
        Guid? reservationId,
        Guid productId,
        string zone,
        Guid locationId,
        decimal quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zone);
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId is required.", nameof(orderId));
        }

        if (orderLineId == Guid.Empty)
        {
            throw new ArgumentException("OrderLineId is required.", nameof(orderLineId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        if (locationId == Guid.Empty)
        {
            throw new ArgumentException("LocationId is required.", nameof(locationId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        return new PickTask
        {
            Id = Guid.CreateVersion7(),
            WaveId = waveId,
            OrderId = orderId,
            OrderLineId = orderLineId,
            ReservationId = reservationId,
            ProductId = productId,
            Zone = zone.Trim(),
            LocationId = locationId,
            Quantity = quantity,
            Status = PickTaskStatus.Pending
        };
    }

    public void BindAllocation(Guid lotId, string lotNo, decimal allocatedQty, decimal shortageQty)
    {
        if (Status is not PickTaskStatus.Pending)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(lotNo);
        if (lotId == Guid.Empty)
        {
            throw new ArgumentException("LotId is required.", nameof(lotId));
        }

        if (allocatedQty <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(allocatedQty), "Allocated quantity must be positive.");
        }

        LotId = lotId;
        LotNo = lotNo.Trim().ToUpperInvariant();
        Quantity = allocatedQty;
        ShortageQty = shortageQty < 0 ? 0 : shortageQty;
    }

    public void MarkShorted(decimal shortageQty)
    {
        if (Status is not PickTaskStatus.Pending)
        {
            return;
        }

        ShortageQty = shortageQty;
        Quantity = 0;
        LotId = null;
        LotNo = null;
        Status = PickTaskStatus.Shorted;
    }

    public void Confirm(Guid scannedLotId, Guid pickerUserId)
    {
        if (Status == PickTaskStatus.Picked)
        {
            return;
        }

        if (Status == PickTaskStatus.Shorted)
        {
            throw new CustomException(
                "Shorted pick tasks cannot be confirmed.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        if (LotId is null)
        {
            throw new CustomException(
                "Pick task has no allocated lot.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        if (scannedLotId != LotId)
        {
            throw new CustomException(
                "Scanned lot does not match the allocated lot.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        if (pickerUserId == Guid.Empty)
        {
            throw new ArgumentException("Picker is required.", nameof(pickerUserId));
        }

        PickerUserId = pickerUserId;
        Status = PickTaskStatus.Picked;
    }

    public bool IsComplete => Status is PickTaskStatus.Picked or PickTaskStatus.Shorted;
}
