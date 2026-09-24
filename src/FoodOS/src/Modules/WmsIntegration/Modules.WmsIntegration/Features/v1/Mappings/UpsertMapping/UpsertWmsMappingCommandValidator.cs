using FluentValidation;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;

namespace FSH.Modules.WmsIntegration.Features.v1.Mappings.UpsertMapping;

public sealed class UpsertWmsMappingCommandValidator : AbstractValidator<UpsertWmsMappingCommand>
{
    public UpsertWmsMappingCommandValidator()
    {
        RuleFor(x => x.Kind)
            .NotEmpty()
            .Must(kind => WmsMappingKinds.All.Any(candidate =>
                string.Equals(candidate, kind.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Unsupported WMS mapping kind.");
        RuleFor(x => x.FoodOsValue).NotEmpty().MaximumLength(160);
        RuleFor(x => x.ExternalValue).NotEmpty().MaximumLength(160);
        RuleFor(x => x.FoodOsQuantityPerExternalUnit)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000);
        RuleFor(x => x)
            .Must(command => string.Equals(command.Kind.Trim(), WmsMappingKinds.Unit, StringComparison.OrdinalIgnoreCase)
                || command.FoodOsQuantityPerExternalUnit == 1)
            .WithMessage("Only unit mappings may define a conversion quantity.");
    }
}
