using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Inventory.Features.v1.Stock;
using Mediator;

namespace FSH.Modules.Inventory.Features.v1.Stock.ReturnInTransitStock;

public sealed class ReturnInTransitStockCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<ReturnInTransitStockCommand, Guid>
{
    public ValueTask<Guid> Handle(ReturnInTransitStockCommand command, CancellationToken cancellationToken)
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
            InventoryTransactionType.ReturnToWarehouse,
            InventoryBucket.InTransit,
            InventoryBucket.OnHand,
            "Return",
            static (balance, qty) => balance.ReturnToWarehouse(qty),
            cancellationToken);
    }
}
