using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;

namespace FSH.Modules.Ordering.Features.v1;

internal static class InventoryStockOps
{
    public static ValueTask<Guid> ReserveAsync(
        IMediator mediator,
        Guid warehouseId,
        TemperatureZoneKind zone,
        Guid productId,
        decimal quantity,
        Guid orderId,
        Guid orderLineId,
        int revision,
        CancellationToken cancellationToken)
    {
        string key = $"order:{orderId:N}:rev{revision}:line:{orderLineId:N}:reserve";
        return mediator.Send(
            new ReserveStockCommand(warehouseId, zone, productId, quantity, orderId, key, orderLineId),
            cancellationToken);
    }

    public static ValueTask<Guid> UnreserveAsync(
        IMediator mediator,
        Guid reservationId,
        Guid orderId,
        Guid orderLineId,
        int revision,
        string action,
        CancellationToken cancellationToken)
    {
        string key = $"order:{orderId:N}:rev{revision}:line:{orderLineId:N}:{action}";
        return mediator.Send(new UnreserveStockCommand(reservationId, key), cancellationToken);
    }
}
