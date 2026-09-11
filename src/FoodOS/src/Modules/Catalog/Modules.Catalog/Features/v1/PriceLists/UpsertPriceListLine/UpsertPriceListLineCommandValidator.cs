using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.UpsertPriceListLine;

public sealed class UpsertPriceListLineCommandValidator : AbstractValidator<UpsertPriceListLineCommand>
{
    public UpsertPriceListLineCommandValidator()
    {
        RuleFor(x => x.PriceListId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.MinQty).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
