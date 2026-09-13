using System.Net;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.AfterSales;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.AfterSales.CreateAfterSalesTicket;

public sealed class CreateAfterSalesTicketCommandHandler(OrderingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<CreateAfterSalesTicketCommand, AfterSalesTicketDto>
{
    public async ValueTask<AfterSalesTicketDto> Handle(
        CreateAfterSalesTicketCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.TryParse<AfterSalesTicketType>(command.Type, ignoreCase: true, out var type))
        {
            throw new CustomException(
                $"Unknown after-sales type '{command.Type}'.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        var order = await dbContext.SalesOrders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {command.OrderId} not found.");

        order.ApplyAfterSales(command.OrderLineId, type, command.Quantity, command.Reason);
        var ticket = AfterSalesTicket.Create(
            order.Id,
            order.StoreId,
            command.OrderLineId,
            type,
            command.Quantity,
            command.Reason,
            currentUser.GetUserId());
        dbContext.AfterSalesTickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ticket.ToDto();
    }
}
