using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Multitenancy.Contracts.Authorization;
using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Multitenancy.Contracts.v1.UpdateTenantTheme;
using Mediator;
using FSH.Framework.Core.Context;
using FSH.Modules.Multitenancy.Services;

namespace FSH.Modules.Multitenancy.Features.v1.UpdateTenantTheme;

public sealed class UpdateTenantThemeCommandHandler(
    ITenantThemeService themeService,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor, ICurrentUser currentUser, ITenantService tenants)
    : ICommandHandler<UpdateTenantThemeCommand>
{
    public async ValueTask<Unit> Handle(UpdateTenantThemeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenantId = await TenantThemeTarget.ResolveAsync(command.TargetTenantId, tenantAccessor, currentUser, tenants, cancellationToken).ConfigureAwait(false);

        await themeService.UpdateThemeAsync(tenantId, command.Theme, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
