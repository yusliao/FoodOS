using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record OrderShipmentLot(
    Guid OrderLineId,
    Guid LotId,
    string LotNo,
    decimal Quantity);

public sealed record StartOrderInTransitCommand(
    Guid OrderId,
    IReadOnlyList<OrderShipmentLot> Lots) : ICommand<Guid>;
