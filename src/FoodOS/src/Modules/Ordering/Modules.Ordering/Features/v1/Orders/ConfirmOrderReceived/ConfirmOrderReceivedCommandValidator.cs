using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Orders;

namespace FSH.Modules.Ordering.Features.v1.Orders.ConfirmOrderReceived;

public sealed class ConfirmOrderReceivedCommandValidator : AbstractValidator<ConfirmOrderReceivedCommand>
{
    public ConfirmOrderReceivedCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Receipts).NotEmpty();
        RuleForEach(x => x.Receipts).ChildRules(line =>
        {
            line.RuleFor(r => r.OrderLineId).NotEmpty();
            line.RuleFor(r => r.LotId).NotEmpty();
            line.RuleFor(r => r.DeliveredQty).GreaterThanOrEqualTo(0);
            line.RuleFor(r => r.ReturnedQty).GreaterThanOrEqualTo(0);
            line.RuleFor(r => r.VarianceReason).MaximumLength(256);
        });
    }
}
