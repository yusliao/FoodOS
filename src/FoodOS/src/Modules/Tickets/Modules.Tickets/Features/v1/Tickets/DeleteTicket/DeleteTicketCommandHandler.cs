using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Context;
using FSH.Modules.Tickets.Contracts.v1.Tickets;
using FSH.Modules.Tickets.Data;
using FSH.Modules.Tickets.Features.v1.Internal;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Tickets.Features.v1.Tickets.DeleteTicket;

public sealed class DeleteTicketCommandHandler(TicketsDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<DeleteTicketCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteTicketCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ticket = await dbContext.Tickets
            .FirstOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Ticket {command.TicketId} not found.");

        ticket.RequireParticipant(currentUser);

        // Soft delete: the audit interceptor converts the EF Delete into an IsDeleted flip.
        // Comments are not auto-included, so they are left untouched and survive a Restore.
        dbContext.Tickets.Remove(ticket);
        await TicketPersistence.SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
