using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Pack;

namespace FSH.Modules.Warehouse.Features.v1.Pack.ListTotesForOrders;

public sealed class ListTotesForOrdersQueryValidator : AbstractValidator<ListTotesForOrdersQuery>
{
    public ListTotesForOrdersQueryValidator()
    {
        RuleFor(x => x.OrderIds).NotNull();
    }
}
