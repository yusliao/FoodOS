using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Inventory.Features.v1.Stock;
using Mediator;

namespace FSH.Modules.Inventory.Features.v1.Stock.ShipPickedStock;

public sealed class ShipPickedStockCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<ShipPickedStockCommand, Guid>
{
    public ValueTask<Guid> Handle(ShipPickedStockCommand command, CancellationToken cancellationToken)
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
            InventoryTransactionType.Ship,
            InventoryBucket.Picked,
            InventoryBucket.InTransit,
            "Ship",
            static (balance, qty) => balance.Ship(qty),
            cancellationToken);
    }
}
