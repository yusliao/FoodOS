using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Stock;

namespace FSH.Modules.Inventory.Features.v1.Stock.ReceiveInventory;

public sealed class ReceiveInventoryCommandValidator : AbstractValidator<ReceiveInventoryCommand>
{
    public ReceiveInventoryCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.LotNo).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Origin).MaximumLength(128);
    }
}
