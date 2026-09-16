using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Tickets.Contracts.Dtos;
using FSH.Modules.Tickets.Contracts.Notifications;
using FSH.Modules.Tickets.Data;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Tickets.Notifications;

public sealed class TicketNotificationAudienceResolver(
    TicketsDbContext db,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor) : ITicketNotificationAudience
{
    public async Task<TicketNotificationAudience?> ResolveAsync(
        Guid ticketId, string customerTenantId, TicketActivityKind activity, CancellationToken cancellationToken)
    {
        var tenantId = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId)
            || (tenantId != MultitenancyConstants.Root.Id && tenantId != customerTenantId))
        {
            return null;
        }
        var ticket = await db.Tickets.AsNoTracking()
            .Where(t => t.Id == ticketId && t.CustomerTenantId == customerTenantId)
            .Select(t => new { t.ReporterUserId, t.AssignedToUserId, t.Status })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (ticket is null) return null;

        var participants = new HashSet<Guid>();
        if (tenantId == customerTenantId) participants.Add(ticket.ReporterUserId);
        if (ticket.AssignedToUserId is { } assignee) participants.Add(assignee);
        participants.Remove(Guid.Empty);
        return new TicketNotificationAudience(participants.ToArray(),
            tenantId == MultitenancyConstants.Root.Id && activity == TicketActivityKind.Created
            && ticket.AssignedToUserId is null && ticket.Status == TicketStatus.Open);
    }
}
