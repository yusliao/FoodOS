using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Stock;

namespace FSH.Modules.Inventory.Features.v1.Stock.ShipPickedStock;

public sealed class ShipPickedStockCommandValidator : AbstractValidator<ShipPickedStockCommand>
{
    public ShipPickedStockCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.LotId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
