using FSH.Framework.Shared.Persistence;
using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record SearchOrdersQuery(
    Guid? StoreId = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<SalesOrderDto>>;
