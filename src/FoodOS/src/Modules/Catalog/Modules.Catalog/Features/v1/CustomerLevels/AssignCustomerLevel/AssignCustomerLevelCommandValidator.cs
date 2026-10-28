using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.AssignCustomerLevel;

public sealed class AssignCustomerLevelCommandValidator : AbstractValidator<AssignCustomerLevelCommand>
{
    public AssignCustomerLevelCommandValidator()
    {
        RuleFor(x => x.CustomerOrgId).NotEmpty();
    }
}
