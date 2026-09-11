using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Orders;

namespace FSH.Modules.Ordering.Features.v1.Orders.PlaceOrder;

public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.StoreId).NotEmpty();
    }
}
