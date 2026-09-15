using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.QuoteProductPrices;

public sealed class QuoteProductPricesQueryValidator : AbstractValidator<QuoteProductPricesQuery>
{
    public QuoteProductPricesQueryValidator()
    {
        RuleFor(query => query.CustomerOrgId).NotEmpty();
        RuleFor(query => query.Products).NotEmpty().Must(products => products.Count <= 100);
        RuleForEach(query => query.Products).ChildRules(product =>
        {
            product.RuleFor(item => item.ProductId).NotEmpty();
            product.RuleFor(item => item.Quantity).GreaterThan(0);
        });
    }
}
