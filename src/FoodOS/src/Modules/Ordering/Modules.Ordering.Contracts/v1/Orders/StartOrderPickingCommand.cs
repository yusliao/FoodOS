using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record StartOrderPickingCommand(Guid OrderId) : ICommand<Guid>;
