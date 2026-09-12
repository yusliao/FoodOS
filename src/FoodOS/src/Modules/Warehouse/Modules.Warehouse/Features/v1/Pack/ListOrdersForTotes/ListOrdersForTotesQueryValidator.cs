using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Pack;

namespace FSH.Modules.Warehouse.Features.v1.Pack.ListOrdersForTotes;

public sealed class ListOrdersForTotesQueryValidator : AbstractValidator<ListOrdersForTotesQuery>
{
    public ListOrdersForTotesQueryValidator()
    {
        RuleFor(x => x.ToteIds).NotNull();
    }
}
