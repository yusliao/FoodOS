using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Domain;

namespace FSH.Modules.Warehouse.Features.v1;

internal static class WarehouseMappings
{
    public static LocationDto ToDto(this Location location)
        => new(location.Id, location.WarehouseId, location.ZoneId, location.Code, location.Type.ToString());

    public static PickTaskDto ToDto(this PickTask task)
        => new(
            task.Id,
            task.WaveId,
            task.OrderId,
            task.OrderLineId,
            task.ProductId,
            task.LocationId,
            task.LotId,
            task.LotNo,
            task.Quantity,
            task.ShortageQty,
            task.Status.ToString());

    public static WaveDto ToDto(this Wave wave)
        => new(
            wave.Id,
            wave.Number,
            wave.DailyPlanId,
            wave.WarehouseId,
            wave.ZoneId,
            wave.Zone,
            wave.BusinessDate,
            wave.Status.ToString(),
            wave.CreatedAt,
            wave.Tasks.Select(t => t.ToDto()).ToList());

    public static PutawayTaskDto ToDto(this PutawayTask task)
        => new(
            task.Id,
            task.WarehouseId,
            task.ZoneId,
            task.Zone,
            task.ProductId,
            task.LotId,
            task.Quantity,
            task.SuggestedLocationId,
            task.LocationId,
            task.Source,
            task.Status.ToString(),
            task.CreatedAt);

    public static PackToteDto ToDto(this PackTote tote, IReadOnlyList<Guid> orderIds)
        => new(
            tote.Id,
            tote.WaveId,
            tote.Sscc,
            tote.DockLocationId,
            tote.Status.ToString(),
            orderIds,
            tote.PackedAt);

    public static ShrinkageDto ToDto(this FSH.Modules.Warehouse.Domain.Shrinkage row)
        => new(
            row.Id,
            row.WarehouseId,
            row.Zone,
            row.ProductId,
            row.LotId,
            row.Quantity,
            row.Reason,
            row.PhotoIds(),
            row.CreatedAt);
}
