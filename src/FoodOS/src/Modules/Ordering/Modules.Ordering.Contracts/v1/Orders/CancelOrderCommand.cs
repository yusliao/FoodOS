using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record CancelOrderCommand(Guid OrderId) : ICommand<Guid>;
