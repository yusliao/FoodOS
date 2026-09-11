using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Orders;

namespace FSH.Modules.Ordering.Features.v1.Orders.ConfirmOrderPacked;

public sealed class ConfirmOrderPackedCommandValidator : AbstractValidator<ConfirmOrderPackedCommand>
{
    public ConfirmOrderPackedCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
