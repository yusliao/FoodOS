using System.Net;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using FSH.Modules.Warehouse.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Waves.AssignWave;

public sealed class AssignWaveCommandHandler(
    WarehouseDbContext dbContext, ICurrentUser currentUser,
    IUserProfileService users, IUserPermissionService permissions) : ICommandHandler<AssignWaveCommand, WaveDto>
{
    public async ValueTask<WaveDto> Handle(AssignWaveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        WaveAccess.RequireOperator(currentUser);
        var wave = await dbContext.Waves.FirstOrDefaultAsync(w => w.Id == command.WaveId, cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("Wave not found.");
        string pickerId = command.PickerUserId.ToString();
        var matches = await users.GetActiveUserIdsAsync([pickerId], cancellationToken).ConfigureAwait(false);
        if (matches.Count != 1) throw new NotFoundException("Picker not found.");
        if (!await permissions.HasPermissionAsync(pickerId, WarehousePermissions.Picks.View, cancellationToken).ConfigureAwait(false)
            || !await permissions.HasPermissionAsync(pickerId, WarehousePermissions.Picks.Confirm, cancellationToken).ConfigureAwait(false))
        {
            throw new CustomException("Assignee must have pick view and confirm permissions.",
                (IEnumerable<string>?)null, HttpStatusCode.Conflict);
        }
        wave.AssignPicker(command.PickerUserId);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CustomException("Wave assignment changed. Reload before retrying.",
                (IEnumerable<string>?)null, HttpStatusCode.Conflict);
        }
        return wave.ToDto();
    }
}
