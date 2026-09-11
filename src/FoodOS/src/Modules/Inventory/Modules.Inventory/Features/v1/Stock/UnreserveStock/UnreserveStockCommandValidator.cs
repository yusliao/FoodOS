using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Stock;

namespace FSH.Modules.Inventory.Features.v1.Stock.UnreserveStock;

public sealed class UnreserveStockCommandValidator : AbstractValidator<UnreserveStockCommand>
{
    public UnreserveStockCommandValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
