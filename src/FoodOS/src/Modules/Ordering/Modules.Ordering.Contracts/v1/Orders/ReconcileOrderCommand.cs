using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record ReconcileOrderCommand(Guid OrderId) : ICommand<Guid>;
