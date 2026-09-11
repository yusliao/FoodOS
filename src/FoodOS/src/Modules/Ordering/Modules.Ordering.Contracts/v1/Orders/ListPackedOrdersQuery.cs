using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record ListPackedOrdersQuery(
    Guid WarehouseId,
    DateOnly BusinessDate,
    IReadOnlyList<Guid> StoreIds) : IQuery<IReadOnlyList<SalesOrderDto>>;
