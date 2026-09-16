using System.Globalization;
using System.Net;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Tickets.Contracts.v1.Tickets;
using FSH.Modules.Tickets.Data;
using FSH.Modules.Tickets.Domain;
using Mediator;
using FSH.Framework.Persistence;
using FSH.Modules.Tickets.Features.v1.Internal;
using Microsoft.EntityFrameworkCore;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Tickets.Contracts.Authorization;

namespace FSH.Modules.Tickets.Features.v1.Tickets.CreateTicket;

public sealed class CreateTicketCommandHandler(
    TicketsDbContext dbContext,
    ICurrentUser currentUser,
    IUserProfileService users,
    IUserPermissionService permissions)
    : ICommandHandler<CreateTicketCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateTicketCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reporterId = currentUser.GetUserId();
        string tenantId = currentUser.GetTenant() ?? throw new UnauthorizedException("Invalid tenant.");
        if (reporterId == Guid.Empty)
        {
            throw new CustomException(
                "Cannot create a ticket without an authenticated reporter.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Unauthorized);
        }

        if (command.AssignedToUserId is not null)
        {
            TicketAccess.RequireOperator(currentUser);
            if (!await permissions.HasPermissionAsync(reporterId.ToString(), TicketsPermissions.Tickets.Assign, cancellationToken)
                .ConfigureAwait(false))
            {
                throw new ForbiddenException("Assign permission is required to create an assigned ticket.");
            }
            await TicketAccess.RequireAssigneeAsync(users, command.AssignedToUserId, cancellationToken).ConfigureAwait(false);
        }

        // Sequential, tenant-scoped ticket numbers (TK-1, …). Count ALL rows incl. soft-deleted so a
        // deleted number isn't reused; racing writers collide on the unique index (→ 409, retryable).
        long count = await dbContext.Tickets
            .IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(ticket => ticket.CustomerTenantId == tenantId)
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
        string number = $"TK-{(count + 1).ToString(CultureInfo.InvariantCulture)}";

        var ticket = Ticket.Create(
            number: number,
            title: command.Title,
            description: command.Description,
            priority: command.Priority,
            reporterUserId: reporterId,
            assignedToUserId: command.AssignedToUserId,
            customerTenantId: tenantId);

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ticket.Id;
    }
}
