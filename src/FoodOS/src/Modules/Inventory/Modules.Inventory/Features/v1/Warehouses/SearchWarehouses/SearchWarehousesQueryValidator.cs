using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.SearchWarehouses;

public sealed class SearchWarehousesQueryValidator : AbstractValidator<SearchWarehousesQuery>
{
    public SearchWarehousesQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Search).MaximumLength(128);
    }
}
