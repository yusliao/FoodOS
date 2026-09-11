using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Orders;

namespace FSH.Modules.Ordering.Features.v1.Orders.LockOrdersForCutoff;

public sealed class LockOrdersForCutoffCommandValidator : AbstractValidator<LockOrdersForCutoffCommand>
{
    public LockOrdersForCutoffCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}
