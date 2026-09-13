using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.AfterSales;

public sealed record CreateAfterSalesTicketCommand(
    Guid OrderId,
    Guid OrderLineId,
    string Type,
    decimal Quantity,
    string Reason) : ICommand<AfterSalesTicketDto>;
