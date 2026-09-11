using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record OrderLineReceipt(
    Guid OrderLineId,
    Guid LotId,
    decimal DeliveredQty,
    decimal ReturnedQty,
    string? VarianceReason = null);

public sealed record ConfirmOrderReceivedCommand(
    Guid OrderId,
    IReadOnlyList<OrderLineReceipt> Receipts) : ICommand<Guid>;
