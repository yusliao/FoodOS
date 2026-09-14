using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Domain;

namespace FSH.Modules.Inventory.Features.v1;

internal static class InventoryLotMappings
{
    public static LotDto ToDto(this Lot lot)
        => new(
            lot.Id,
            lot.LotNo,
            lot.ProductId,
            lot.SupplierId,
            lot.ManufacturedOn,
            lot.ExpiryDate,
            lot.Origin,
            lot.Status.ToString(),
            lot.CreatedAtUtc);

    public static LotBalanceDto ToDto(this LotBalance balance, string zoneKind)
        => new(
            balance.Id,
            balance.WarehouseId,
            balance.ZoneId,
            zoneKind,
            balance.LotId,
            balance.ProductId,
            balance.OnHand,
            balance.Reserved,
            balance.Allocated,
            balance.Picked,
            balance.InTransit,
            balance.Isolated,
            balance.Available);

    public static InventoryTransactionDto ToDto(this InventoryTransaction tx)
        => new(
            tx.Id,
            tx.Type.ToString(),
            tx.ProductId,
            tx.WarehouseId,
            tx.ZoneId,
            tx.LotId,
            tx.Quantity,
            tx.FromBucket?.ToString(),
            tx.ToBucket?.ToString(),
            tx.RefType,
            tx.RefId,
            tx.OccurredAt);
}
