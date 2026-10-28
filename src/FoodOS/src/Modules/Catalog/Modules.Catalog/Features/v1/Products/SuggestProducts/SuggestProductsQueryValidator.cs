using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.Products;

namespace FSH.Modules.Catalog.Features.v1.Products.SuggestProducts;

public sealed class SuggestProductsQueryValidator : AbstractValidator<SuggestProductsQuery>
{
    public SuggestProductsQueryValidator()
    {
        RuleFor(query => query.Term).NotEmpty().MaximumLength(100);
        RuleFor(query => query.Limit).InclusiveBetween(1, 20);
    }
}
