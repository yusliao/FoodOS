using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Shop;

namespace FSH.Modules.Ordering.Features.v1.Shop.SearchShopProducts;

public sealed class SearchShopProductsQueryValidator : AbstractValidator<SearchShopProductsQuery>
{
    public SearchShopProductsQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
