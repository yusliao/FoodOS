using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Multitenancy.Contracts.Dtos;
using FSH.Modules.Multitenancy.Contracts.v1.GetTenantTheme;
using Mediator;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Multitenancy.Services;

namespace FSH.Modules.Multitenancy.Features.v1.GetTenantTheme;

public sealed class GetTenantThemeQueryHandler(ITenantThemeService themeService,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor, ICurrentUser currentUser, ITenantService tenants)
    : IQueryHandler<GetTenantThemeQuery, TenantThemeDto>
{
    public async ValueTask<TenantThemeDto> Handle(GetTenantThemeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var target = await TenantThemeTarget.ResolveAsync(query.TargetTenantId, tenantAccessor, currentUser, tenants, cancellationToken).ConfigureAwait(false);
        return await themeService.GetThemeAsync(target, cancellationToken).ConfigureAwait(false);
    }
}
