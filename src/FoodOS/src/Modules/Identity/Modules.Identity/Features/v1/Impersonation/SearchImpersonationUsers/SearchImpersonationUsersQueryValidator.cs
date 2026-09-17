using FluentValidation;
using FSH.Modules.Identity.Contracts.v1.Impersonation;

namespace FSH.Modules.Identity.Features.v1.Impersonation.SearchImpersonationUsers;

public sealed class SearchImpersonationUsersQueryValidator : AbstractValidator<SearchImpersonationUsersQuery>
{
    public SearchImpersonationUsersQueryValidator()
    {
        RuleFor(x => x.TargetTenantId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Search).MaximumLength(256);
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Sort).Empty();
    }
}
