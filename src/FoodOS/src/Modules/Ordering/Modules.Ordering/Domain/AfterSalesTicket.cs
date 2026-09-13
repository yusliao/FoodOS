using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

public sealed class AfterSalesTicket : AggregateRoot<Guid>
{
    public Guid OrderId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid OrderLineId { get; private set; }
    public AfterSalesTicketType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public string Reason { get; private set; } = default!;
    public AfterSalesTicketStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AfterSalesTicket() { }

    public static AfterSalesTicket Create(
        Guid orderId,
        Guid storeId,
        Guid orderLineId,
        AfterSalesTicketType type,
        decimal quantity,
        string reason,
        Guid createdByUserId)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId is required.", nameof(orderId));
        }

        if (storeId == Guid.Empty)
        {
            throw new ArgumentException("StoreId is required.", nameof(storeId));
        }

        if (orderLineId == Guid.Empty)
        {
            throw new ArgumentException("OrderLineId is required.", nameof(orderLineId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        }

        return new AfterSalesTicket
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            StoreId = storeId,
            OrderLineId = orderLineId,
            Type = type,
            Quantity = quantity,
            Reason = reason.Trim(),
            Status = AfterSalesTicketStatus.Applied,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
