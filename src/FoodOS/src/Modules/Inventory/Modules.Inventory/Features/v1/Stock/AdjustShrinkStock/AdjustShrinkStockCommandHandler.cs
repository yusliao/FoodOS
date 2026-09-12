using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Inventory.Features.v1.Stock;
using Mediator;

namespace FSH.Modules.Inventory.Features.v1.Stock.AdjustShrinkStock;

public sealed class AdjustShrinkStockCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<AdjustShrinkStockCommand, Guid>
{
    public ValueTask<Guid> Handle(AdjustShrinkStockCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return LotBalanceStockMover.MoveAsync(
            dbContext,
            command.WarehouseId,
            command.Zone,
            command.ProductId,
            command.LotId,
            command.Quantity,
            command.IdempotencyKey,
            command.RefId,
            InventoryTransactionType.AdjustShrink,
            InventoryBucket.OnHand,
            toBucket: null,
            "Shrinkage",
            static (balance, qty) => balance.Shrink(qty),
            cancellationToken);
    }
}
