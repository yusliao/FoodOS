using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Context;
using FSH.Modules.Tickets.Features.v1.Internal;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Tickets.Contracts.v1.Tickets;
using FSH.Modules.Tickets.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Tickets.Features.v1.Tickets.AssignTicket;

public sealed class AssignTicketCommandHandler(TicketsDbContext dbContext, ICurrentUser currentUser, IUserProfileService users)
    : ICommandHandler<AssignTicketCommand, Guid>
{
    public async ValueTask<Guid> Handle(AssignTicketCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        TicketAccess.RequireOperator(currentUser);

        var ticket = await dbContext.Tickets
            .FirstOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Ticket {command.TicketId} not found.");

        ticket.Assign(command.AssigneeUserId);
        await TicketAccess.RequireAssigneeAsync(users, command.AssigneeUserId, cancellationToken).ConfigureAwait(false);
        await TicketPersistence.SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
        return ticket.Id;
    }
}
