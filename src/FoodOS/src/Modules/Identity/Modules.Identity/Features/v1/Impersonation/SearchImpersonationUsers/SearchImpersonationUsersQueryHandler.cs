using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Contracts.v1.Impersonation;
using FSH.Modules.Identity.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Identity.Features.v1.Impersonation.SearchImpersonationUsers;

public sealed class SearchImpersonationUsersQueryHandler(
    IdentityDbContext db,
    ICurrentUser user,
    IMultiTenantStore<AppTenantInfo> tenants,
    IOptions<TenantGraceOptions> grace,
    TimeProvider clock)
    : IQueryHandler<SearchImpersonationUsersQuery, PagedResponse<UserDto>>
{
    public async ValueTask<PagedResponse<UserDto>> Handle(SearchImpersonationUsersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!user.IsAuthenticated() || user.GetTenant() != MultitenancyConstants.Root.Id
            || user.GetUserClaims()?.Any(c => c.Type == ClaimConstants.ActorSubject) == true)
            throw new ForbiddenException("Support user lookup is restricted to non-impersonating root operators.");

        var tenant = await tenants.GetAsync(query.TargetTenantId).ConfigureAwait(false)
            ?? throw new NotFoundException("target tenant not found");
        if (!tenant.IsActive || (tenant.Id != MultitenancyConstants.Root.Id
            && tenant.ValidUpto.AddDays(grace.Value.GraceWindowDays) < clock.GetUtcNow().UtcDateTime))
            throw new ForbiddenException("target tenant is unavailable");
        if (!string.IsNullOrWhiteSpace(tenant.ConnectionString))
            throw new ForbiddenException("cross-database impersonation is not supported");

        var actorId = user.GetUserId().ToString();
        // Dedicated authorized support lookup: restore explicit tenant isolation after bypassing
        // Finbuckle's current-root filter. FshUser has no soft-delete filter.
        var users = db.Users.AsNoTracking().IgnoreQueryFilters()
            .Where(u => EF.Property<string>(u, "TenantId") == query.TargetTenantId
                && u.IsActive && u.Id != actorId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            users = users.Where(u => (u.UserName != null && u.UserName.Contains(search))
                || (u.Email != null && u.Email.Contains(search))
                || (u.FirstName != null && u.FirstName.Contains(search))
                || (u.LastName != null && u.LastName.Contains(search)));
        }
        return await users.OrderBy(u => u.UserName).ThenBy(u => u.Id)
            .Select(u => new UserDto
            {
                Id = u.Id, UserName = u.UserName, FirstName = u.FirstName,
                LastName = u.LastName, Email = u.Email, IsActive = u.IsActive,
                EmailConfirmed = u.EmailConfirmed,
            }).ToPagedResponseAsync(query, cancellationToken).ConfigureAwait(false);
    }
}
