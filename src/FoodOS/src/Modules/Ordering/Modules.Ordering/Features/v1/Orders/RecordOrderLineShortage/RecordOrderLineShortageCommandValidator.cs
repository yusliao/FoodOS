using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Orders;

namespace FSH.Modules.Ordering.Features.v1.Orders.RecordOrderLineShortage;

public sealed class RecordOrderLineShortageCommandValidator : AbstractValidator<RecordOrderLineShortageCommand>
{
    public RecordOrderLineShortageCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.OrderLineId).NotEmpty();
        RuleFor(x => x.ShortageQty).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(128);
    }
}
