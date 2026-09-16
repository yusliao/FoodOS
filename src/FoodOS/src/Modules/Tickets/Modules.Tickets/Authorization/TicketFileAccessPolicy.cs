using FSH.Framework.Core.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Files.Contracts;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Tickets.Contracts.Authorization;
using FSH.Modules.Tickets.Data;
using FSH.Modules.Tickets.Features.v1.Internal;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Tickets.Authorization;

public sealed class TicketFileAccessPolicy(
    TicketsDbContext dbContext,
    ICurrentUser currentUser,
    IUserPermissionService permissions) : ICrossTenantFileReadPolicy
{
    public string OwnerType => "Ticket";
    public bool AllowsPublicFiles => false;

    public async Task<bool> CanReadAcrossTenantsAsync(
        FileAccessContext context, string fileTenantId, string currentUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Visibility != 1 || !string.Equals(context.OwnerType, OwnerType, StringComparison.OrdinalIgnoreCase)
            || !await CanAccessTicketAsync(context.OwnerId, currentUserId, cancellationToken).ConfigureAwait(false))
            return false;

        var customerTenant = await dbContext.Tickets.AsNoTracking().ApplyParticipantScope(currentUser)
            .Where(t => t.Id == context.OwnerId).Select(t => t.CustomerTenantId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return customerTenant is not null && (string.Equals(fileTenantId, customerTenant, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileTenantId, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase));
    }

    public Task<bool> CanAttachAsync(Guid? ownerId, string currentUserId, CancellationToken cancellationToken)
        => CanAccessTicketAsync(ownerId, currentUserId, cancellationToken);

    public Task<bool> CanReadAsync(
        FileAccessContext context,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return CanAccessTicketAsync(context.OwnerId, currentUserId, cancellationToken);
    }

    public async Task<bool> CanDeleteAsync(
        FileAccessContext context,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return string.Equals(context.CreatedByUserId, currentUserId, StringComparison.Ordinal)
            && await CanAccessTicketAsync(context.OwnerId, currentUserId, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CanChangeVisibilityAsync(
        FileAccessContext context,
        string currentUserId,
        CancellationToken cancellationToken)
        => Task.FromResult(false);

    private async Task<bool> CanAccessTicketAsync(
        Guid? ticketId,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        if (ticketId is null || !Guid.TryParse(currentUserId, out Guid userId)
            || userId == Guid.Empty || userId != currentUser.GetUserId()
            || !currentUser.IsAuthenticated())
        {
            return false;
        }

        if (!await permissions.HasPermissionAsync(currentUserId, TicketsPermissions.Tickets.View, cancellationToken)
            .ConfigureAwait(false))
        {
            return false;
        }

        return await dbContext.Tickets.AsNoTracking().ApplyParticipantScope(currentUser)
            .AnyAsync(ticket => ticket.Id == ticketId.Value, cancellationToken).ConfigureAwait(false);
    }
}
