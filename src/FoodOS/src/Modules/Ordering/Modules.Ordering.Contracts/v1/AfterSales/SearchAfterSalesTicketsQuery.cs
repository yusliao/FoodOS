using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.AfterSales;

public sealed record SearchAfterSalesTicketsQuery(Guid StoreId, Guid? OrderId = null)
    : IQuery<IReadOnlyList<AfterSalesTicketDto>>;
