namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record WarehouseOrderFeedbackLine(Guid OrderLineId, decimal Quantity);

// UnresolvedLineCount: event lines whose lineId is not a FoodOS order line id. They are reported so
// a shortage is never silently dropped.
public sealed record WarehouseOrderFeedback(
    Guid OrderId,
    string EventType,
    string? ReasonCode,
    DateTimeOffset OccurredAt,
    IReadOnlyList<WarehouseOrderFeedbackLine> Lines,
    int UnresolvedLineCount = 0);

public interface IWarehouseOrderFeedbackSink
{
    Task ApplyAsync(WarehouseOrderFeedback feedback, CancellationToken cancellationToken = default);
}
