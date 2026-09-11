using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record LockOrdersForCutoffCommand(Guid WarehouseId, DateOnly BusinessDate) : ICommand<int>;
