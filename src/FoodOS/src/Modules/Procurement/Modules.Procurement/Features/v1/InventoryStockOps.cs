using FSH.Modules.Inventory.Contracts;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using Mediator;

namespace FSH.Modules.Procurement.Features.v1;

internal static class InventoryStockOps
{
    public static ValueTask<Guid> ReceiveAsync(
        IMediator mediator,
        Guid warehouseId,
        TemperatureZoneKind zone,
        Guid productId,
        string lotNo,
        DateOnly expiryDate,
        decimal quantity,
        Guid purchaseOrderId,
        Guid lineId,
        DateOnly? manufacturedOn,
        string? origin,
        CancellationToken cancellationToken)
    {
        string key = $"po:{purchaseOrderId:N}:line:{lineId:N}:lot:{lotNo.Trim().ToUpperInvariant()}:receive";
        return mediator.Send(
            new ReceiveInventoryCommand(
                warehouseId,
                zone,
                productId,
                lotNo,
                expiryDate,
                quantity,
                key,
                manufacturedOn,
                origin),
            cancellationToken);
    }

    public static ValueTask<Guid> ReceiveIsolatedAsync(
        IMediator mediator,
        Guid warehouseId,
        TemperatureZoneKind zone,
        Guid productId,
        string lotNo,
        DateOnly expiryDate,
        decimal quantity,
        Guid purchaseOrderId,
        Guid lineId,
        DateOnly? manufacturedOn,
        string? origin,
        Guid? supplierId,
        CancellationToken cancellationToken)
    {
        string key = $"po:{purchaseOrderId:N}:line:{lineId:N}:lot:{lotNo.Trim().ToUpperInvariant()}:receive-isolated";
        return mediator.Send(
            new ReceiveIsolatedStockCommand(
                warehouseId,
                zone,
                productId,
                lotNo,
                expiryDate,
                quantity,
                key,
                manufacturedOn,
                origin,
                supplierId),
            cancellationToken);
    }
}
