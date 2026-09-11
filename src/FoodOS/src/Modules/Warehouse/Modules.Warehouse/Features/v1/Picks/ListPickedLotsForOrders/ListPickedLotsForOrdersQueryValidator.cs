using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Picks;

namespace FSH.Modules.Warehouse.Features.v1.Picks.ListPickedLotsForOrders;

public sealed class ListPickedLotsForOrdersQueryValidator : AbstractValidator<ListPickedLotsForOrdersQuery>
{
    public ListPickedLotsForOrdersQueryValidator()
    {
        RuleFor(x => x.OrderIds).NotNull();
    }
}
