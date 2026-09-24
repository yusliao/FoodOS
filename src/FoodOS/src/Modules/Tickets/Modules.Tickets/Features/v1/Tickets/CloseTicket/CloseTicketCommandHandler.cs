using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Context;
using FSH.Modules.Tickets.Contracts.v1.Tickets;
using FSH.Modules.Tickets.Data;
using FSH.Modules.Tickets.Features.v1.Internal;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Tickets.Features.v1.Tickets.CloseTicket;

public sealed class CloseTicketCommandHandler(TicketsDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<CloseTicketCommand, Guid>
{
    public async ValueTask<Guid> Handle(CloseTicketCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ticket = await dbContext.Tickets
            .FirstOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Ticket {command.TicketId} not found.");

        ticket.RequireParticipant(currentUser);

        ticket.Close();
        await TicketPersistence.SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
        return ticket.Id;
    }
}
