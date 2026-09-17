using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Multitenancy.Contracts.Authorization;
using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Multitenancy.Contracts.v1.ResetTenantTheme;
using Mediator;
using FSH.Framework.Core.Context;
using FSH.Modules.Multitenancy.Services;

namespace FSH.Modules.Multitenancy.Features.v1.ResetTenantTheme;

public sealed class ResetTenantThemeCommandHandler(
    ITenantThemeService themeService,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor, ICurrentUser currentUser, ITenantService tenants)
    : ICommandHandler<ResetTenantThemeCommand>
{
    public async ValueTask<Unit> Handle(ResetTenantThemeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var tenantId = await TenantThemeTarget.ResolveAsync(command.TargetTenantId, tenantAccessor, currentUser, tenants, cancellationToken).ConfigureAwait(false);

        await themeService.ResetThemeAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
