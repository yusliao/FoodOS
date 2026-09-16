using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Tickets.Domain;

namespace FSH.Modules.Tickets.Features.v1.Internal;

internal static class TicketAccess
{
    public static IQueryable<Ticket> ApplyParticipantScope(
        this IQueryable<Ticket> tickets,
        ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(currentUser);
        if (!currentUser.IsAuthenticated() || currentUser.GetUserId() == Guid.Empty)
        {
            return tickets.Where(ticket => false);
        }
        if (IsOperator(currentUser))
        {
            return tickets;
        }

        Guid userId = currentUser.GetUserId();
        return tickets.Where(ticket =>
            ticket.ReporterUserId == userId || ticket.AssignedToUserId == userId);
    }

    public static void RequireParticipant(this Ticket ticket, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(currentUser);
        if (!currentUser.IsAuthenticated() || currentUser.GetUserId() == Guid.Empty)
        {
            throw new NotFoundException($"Ticket {ticket.Id} not found.");
        }
        if (IsOperator(currentUser))
        {
            return;
        }

        Guid userId = currentUser.GetUserId();
        if (ticket.ReporterUserId != userId && ticket.AssignedToUserId != userId)
        {
            throw new NotFoundException($"Ticket {ticket.Id} not found.");
        }
    }

    public static bool IsOperator(ICurrentUser currentUser)
        => string.Equals(
            currentUser.GetTenant(),
            MultitenancyConstants.Root.Id,
            StringComparison.OrdinalIgnoreCase);
}
