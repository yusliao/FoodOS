using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.DeletePriceListLine;

public sealed class DeletePriceListLineCommandValidator : AbstractValidator<DeletePriceListLineCommand>
{
    public DeletePriceListLineCommandValidator()
    {
        RuleFor(x => x.PriceListId).NotEmpty();
        RuleFor(x => x.LineId).NotEmpty();
    }
}
