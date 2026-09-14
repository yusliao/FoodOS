using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Lots;

namespace FSH.Modules.Inventory.Features.v1.Lots.SearchLots;

public sealed class SearchLotsQueryValidator : AbstractValidator<SearchLotsQuery>
{
    public SearchLotsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.LotNo).MaximumLength(64);
        RuleFor(x => x.Status).MaximumLength(16);
    }
}
