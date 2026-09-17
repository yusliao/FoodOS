using FSH.Modules.Logistics.Contracts.v1.Drivers;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.Services;
using System.Net;

namespace FSH.Modules.Logistics.Features.v1.Drivers.CreateDriver;

public sealed class CreateDriverCommandHandler(
    LogisticsDbContext dbContext,
    IUserProfileService users,
    IMultiTenantContextAccessor<AppTenantInfo> tenantContext)
    : ICommandHandler<CreateDriverCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateDriverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (tenantContext.MultiTenantContext.TenantInfo?.Id != MultitenancyConstants.Root.Id)
        {
            throw new CustomException("Driver registration requires the operator identity domain.",
                (IEnumerable<string>?)null, HttpStatusCode.Forbidden);
        }

        // Validate before replay lookup as an existing binding does not prove the user is still eligible.
        var eligible = await users.GetActiveUserIdsAsync([command.UserId.ToString()], cancellationToken)
            .ConfigureAwait(false);
        if (eligible.Count == 0)
        {
            throw new CustomException("Select an active operator user for the driver.",
                (IEnumerable<string>?)null, HttpStatusCode.BadRequest);
        }

        var existing = await dbContext.Drivers
            .FirstOrDefaultAsync(d => d.UserId == command.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Id;
        }

        var driver = Driver.Create(command.UserId, command.Phone);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return driver.Id;
    }
}
