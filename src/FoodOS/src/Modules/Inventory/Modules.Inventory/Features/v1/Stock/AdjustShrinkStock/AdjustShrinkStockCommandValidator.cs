using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Stock;

namespace FSH.Modules.Inventory.Features.v1.Stock.AdjustShrinkStock;

public sealed class AdjustShrinkStockCommandValidator : AbstractValidator<AdjustShrinkStockCommand>
{
    public AdjustShrinkStockCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.LotId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
