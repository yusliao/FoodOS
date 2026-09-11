using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Orders;

namespace FSH.Modules.Ordering.Features.v1.Orders.ReconcileOrder;

public sealed class ReconcileOrderCommandValidator : AbstractValidator<ReconcileOrderCommand>
{
    public ReconcileOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
