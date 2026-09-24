using FluentValidation;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;

namespace FSH.Modules.WmsIntegration.Features.v1.Mappings.ValidateMappings;

public sealed class ValidateWmsMappingsQueryValidator : AbstractValidator<ValidateWmsMappingsQuery>
{
    public ValidateWmsMappingsQueryValidator()
    {
        RuleFor(x => x.Requirements).NotNull().NotEmpty().Must(x => x.Count <= 500);
        RuleForEach(x => x.Requirements).ChildRules(requirement =>
        {
            requirement.RuleFor(x => x.Kind)
                .NotEmpty()
                .Must(kind => WmsMappingKinds.All.Any(candidate =>
                    string.Equals(candidate, kind.Trim(), StringComparison.OrdinalIgnoreCase)))
                .WithMessage("Unsupported WMS mapping kind.");
            requirement.RuleFor(x => x.FoodOsValue).NotEmpty().MaximumLength(160);
        });
    }
}
