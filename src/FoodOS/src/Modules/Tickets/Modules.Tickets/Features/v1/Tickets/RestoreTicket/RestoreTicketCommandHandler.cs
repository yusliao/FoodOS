using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Context;
using FSH.Framework.Persistence;
using FSH.Modules.Tickets.Contracts.v1.Tickets;
using FSH.Modules.Tickets.Data;
using FSH.Modules.Tickets.Features.v1.Internal;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Tickets.Features.v1.Tickets.RestoreTicket;

public sealed class RestoreTicketCommandHandler(TicketsDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<RestoreTicketCommand, Guid>
{
    public async ValueTask<Guid> Handle(RestoreTicketCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ticket = await dbContext.Tickets
            .IgnoreQueryFilters([QueryFilters.SoftDelete])
            .FirstOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Ticket {command.TicketId} not found.");

        ticket.RequireParticipant(currentUser);

        ticket.Restore();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ticket.Id;
    }
}
