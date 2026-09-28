using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;

namespace FSH.Modules.Ordering.Features.v1.CustomerOrgs.UpdateCustomerOrg;

public sealed class UpdateCustomerOrgCommandValidator : AbstractValidator<UpdateCustomerOrgCommand>
{
    public UpdateCustomerOrgCommandValidator()
    {
        RuleFor(command => command.CustomerOrgId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(128);
    }
}
