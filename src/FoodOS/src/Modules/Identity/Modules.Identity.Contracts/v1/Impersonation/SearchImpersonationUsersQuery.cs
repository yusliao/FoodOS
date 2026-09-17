using FSH.Framework.Shared.Persistence;
using FSH.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace FSH.Modules.Identity.Contracts.v1.Impersonation;

public sealed class SearchImpersonationUsersQuery : IPagedQuery, IQuery<PagedResponse<UserDto>>
{
    public string TargetTenantId { get; set; } = string.Empty;
    public string? Search { get; set; }
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 25;
    public string? Sort { get; set; }
}
