using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Domain;

namespace FSH.Modules.Warehouse.Features.v1;

internal static class WaveAccess
{
    public static void RequireOperator(ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated() || currentUser.GetUserId() == Guid.Empty
            || !string.Equals(currentUser.GetTenant(), MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Wave operations require an operator identity.");
    }

    public static async Task<IQueryable<Wave>> ApplyReadScopeAsync(
        this IQueryable<Wave> waves, ICurrentUser currentUser, IUserPermissionService permissions,
        CancellationToken cancellationToken)
    {
        RequireOperator(currentUser);
        Guid userId = currentUser.GetUserId();
        bool supervisor = await permissions.HasPermissionAsync(userId.ToString(), WarehousePermissions.Waves.Assign,
            cancellationToken).ConfigureAwait(false);
        return supervisor ? waves : waves.Where(w => w.AssignedPickerUserId == userId);
    }
}
