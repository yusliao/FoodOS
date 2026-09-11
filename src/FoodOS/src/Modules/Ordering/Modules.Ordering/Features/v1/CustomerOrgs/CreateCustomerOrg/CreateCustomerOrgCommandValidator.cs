using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;

namespace FSH.Modules.Ordering.Features.v1.CustomerOrgs.CreateCustomerOrg;

public sealed class CreateCustomerOrgCommandValidator : AbstractValidator<CreateCustomerOrgCommand>
{
    public CreateCustomerOrgCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}
