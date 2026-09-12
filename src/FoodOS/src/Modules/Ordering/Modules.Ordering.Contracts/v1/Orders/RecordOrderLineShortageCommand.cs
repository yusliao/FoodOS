using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record RecordOrderLineShortageCommand(
    Guid OrderId,
    Guid OrderLineId,
    decimal ShortageQty,
    string Reason) : ICommand<Guid>;
