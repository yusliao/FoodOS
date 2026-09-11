using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record PlaceOrderCommand(Guid StoreId) : ICommand<Guid>;
