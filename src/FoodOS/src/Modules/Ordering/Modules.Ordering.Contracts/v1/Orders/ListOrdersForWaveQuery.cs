using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record ListOrdersForWaveQuery(Guid WarehouseId, DateOnly BusinessDate)
    : IQuery<IReadOnlyList<SalesOrderDto>>;
