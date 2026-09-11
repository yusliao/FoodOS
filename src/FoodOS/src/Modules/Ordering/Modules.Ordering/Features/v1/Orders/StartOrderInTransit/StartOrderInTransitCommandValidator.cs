using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Orders;

namespace FSH.Modules.Ordering.Features.v1.Orders.StartOrderInTransit;

public sealed class StartOrderInTransitCommandValidator : AbstractValidator<StartOrderInTransitCommand>
{
    public StartOrderInTransitCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Lots).NotNull();
        RuleForEach(x => x.Lots).ChildRules(lot =>
        {
            lot.RuleFor(l => l.OrderLineId).NotEmpty();
            lot.RuleFor(l => l.LotId).NotEmpty();
            lot.RuleFor(l => l.LotNo).NotEmpty().MaximumLength(64);
            lot.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}
