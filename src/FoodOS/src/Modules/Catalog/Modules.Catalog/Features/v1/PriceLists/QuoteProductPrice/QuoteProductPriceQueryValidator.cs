using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.QuoteProductPrice;

public sealed class QuoteProductPriceQueryValidator : AbstractValidator<QuoteProductPriceQuery>
{
    public QuoteProductPriceQueryValidator()
    {
        RuleFor(x => x.CustomerOrgId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
