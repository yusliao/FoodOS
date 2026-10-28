using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.UpdateCustomerLevel;

public sealed class UpdateCustomerLevelCommandValidator : AbstractValidator<UpdateCustomerLevelCommand>
{
    public UpdateCustomerLevelCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DiscountPercent).GreaterThanOrEqualTo(0).LessThan(100).PrecisionScale(5, 2, true);
    }
}
