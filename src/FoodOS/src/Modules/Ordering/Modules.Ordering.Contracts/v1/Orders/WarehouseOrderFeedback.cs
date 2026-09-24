namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record WarehouseOrderFeedbackLine(Guid OrderLineId, decimal Quantity);

public sealed record WarehouseOrderFeedback(
    Guid OrderId,
    string EventType,
    string? ReasonCode,
    DateTimeOffset OccurredAt,
    IReadOnlyList<WarehouseOrderFeedbackLine> Lines);

public interface IWarehouseOrderFeedbackSink
{
    Task ApplyAsync(WarehouseOrderFeedback feedback, CancellationToken cancellationToken = default);
}
