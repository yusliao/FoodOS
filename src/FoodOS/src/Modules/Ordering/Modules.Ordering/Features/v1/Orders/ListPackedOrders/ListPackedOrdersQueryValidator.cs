using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Orders;

namespace FSH.Modules.Ordering.Features.v1.Orders.ListPackedOrders;

public sealed class ListPackedOrdersQueryValidator : AbstractValidator<ListPackedOrdersQuery>
{
    public ListPackedOrdersQueryValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.StoreIds).NotNull();
    }
}
