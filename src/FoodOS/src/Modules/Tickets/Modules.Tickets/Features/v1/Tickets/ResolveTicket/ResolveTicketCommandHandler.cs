using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Context;
using FSH.Modules.Tickets.Features.v1.Internal;
using FSH.Modules.Tickets.Contracts.v1.Tickets;
using FSH.Modules.Tickets.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Tickets.Features.v1.Tickets.ResolveTicket;

public sealed class ResolveTicketCommandHandler(TicketsDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<ResolveTicketCommand, Guid>
{
    public async ValueTask<Guid> Handle(ResolveTicketCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        TicketAccess.RequireOperator(currentUser);

        var ticket = await dbContext.Tickets
            .FirstOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Ticket {command.TicketId} not found.");

        ticket.Resolve(command.ResolutionNote);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ticket.Id;
    }
}
