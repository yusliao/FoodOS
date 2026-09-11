using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Orders;

namespace FSH.Modules.Ordering.Features.v1.Orders.StartOrderPicking;

public sealed class StartOrderPickingCommandValidator : AbstractValidator<StartOrderPickingCommand>
{
    public StartOrderPickingCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
