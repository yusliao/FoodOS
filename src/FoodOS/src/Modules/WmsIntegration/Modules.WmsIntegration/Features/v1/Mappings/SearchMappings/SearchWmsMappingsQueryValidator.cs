using FluentValidation;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;

namespace FSH.Modules.WmsIntegration.Features.v1.Mappings.SearchMappings;

public sealed class SearchWmsMappingsQueryValidator : AbstractValidator<SearchWmsMappingsQuery>
{
    public SearchWmsMappingsQueryValidator()
    {
        RuleFor(x => x.Kind)
            .Must(kind => string.IsNullOrWhiteSpace(kind)
                || WmsMappingKinds.All.Any(candidate =>
                    string.Equals(candidate, kind.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Unsupported WMS mapping kind.");
        RuleFor(x => x.Search).MaximumLength(160);
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
