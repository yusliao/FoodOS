using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Inventory.Features.v1.Stock;
using Mediator;

namespace FSH.Modules.Inventory.Features.v1.Stock.DeliverInTransitStock;

public sealed class DeliverInTransitStockCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<DeliverInTransitStockCommand, Guid>
{
    public ValueTask<Guid> Handle(DeliverInTransitStockCommand command, CancellationToken cancellationToken)
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
            InventoryTransactionType.Deliver,
            InventoryBucket.InTransit,
            toBucket: null,
            "Deliver",
            static (balance, qty) => balance.Deliver(qty),
            cancellationToken);
    }
}
