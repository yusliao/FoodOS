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

public sealed class GetShopProductByIdQueryValidator : AbstractValidator<GetShopProductByIdQuery>
{
    public GetShopProductByIdQueryValidator()
    {
        RuleFor(query => query.ProductId).NotEmpty();
        RuleFor(query => query.Quantity).GreaterThan(0);
    }
}
