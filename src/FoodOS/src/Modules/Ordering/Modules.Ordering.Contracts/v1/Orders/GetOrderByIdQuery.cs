using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record GetOrderByIdQuery(Guid OrderId) : IQuery<SalesOrderDto>;
