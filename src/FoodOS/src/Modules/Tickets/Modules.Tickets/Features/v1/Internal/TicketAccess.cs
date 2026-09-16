using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Tickets.Domain;
using FSH.Modules.Identity.Contracts.Services;

namespace FSH.Modules.Tickets.Features.v1.Internal;

internal static class TicketAccess
{
    public static async Task RequireAssigneeAsync(IUserProfileService users, Guid? assigneeId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(users);
        if (assigneeId is not { } id) return;
        var matches = await users.GetActiveUserIdsAsync([id.ToString()], cancellationToken).ConfigureAwait(false);
        if (matches.Count != 1) throw new NotFoundException("Assignee not found.");
    }

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
        string? tenantId = currentUser.GetTenant();
        return tickets.Where(ticket =>
            ticket.CustomerTenantId == tenantId
            && (ticket.ReporterUserId == userId || ticket.AssignedToUserId == userId));
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
        if (!string.Equals(ticket.CustomerTenantId, currentUser.GetTenant(), StringComparison.Ordinal)
            || (ticket.ReporterUserId != userId && ticket.AssignedToUserId != userId))
        {
            throw new NotFoundException($"Ticket {ticket.Id} not found.");
        }
    }

    public static void RequireOperator(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        if (!currentUser.IsAuthenticated() || currentUser.GetUserId() == Guid.Empty || !IsOperator(currentUser))
        {
            throw new ForbiddenException("This ticket action requires an operator identity.");
        }
    }

    public static bool IsOperator(ICurrentUser currentUser)
        => string.Equals(
            currentUser.GetTenant(),
            MultitenancyConstants.Root.Id,
            StringComparison.OrdinalIgnoreCase);
}
