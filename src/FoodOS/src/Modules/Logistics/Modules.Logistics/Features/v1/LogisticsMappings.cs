using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Domain;

namespace FSH.Modules.Logistics.Features.v1;

internal static class LogisticsMappings
{
    public static VehicleDto ToDto(this Vehicle vehicle)
        => new(vehicle.Id, vehicle.Plate, vehicle.CompartmentZones, vehicle.PayloadKg);

    public static DriverDto ToDto(this Driver driver)
        => new(driver.Id, driver.UserId, driver.Phone);

    public static RouteDto ToDto(this Route route)
        => new(route.Id, route.WarehouseId, route.Code, route.GetStoreIds(), route.DefaultVehicleId);

    public static ShipmentDto ToDto(this Shipment shipment)
        => new(
            shipment.Id,
            shipment.Number,
            shipment.RouteId,
            shipment.WarehouseId,
            shipment.BusinessDate,
            shipment.VehicleId,
            shipment.DriverId,
            shipment.Status.ToString(),
            shipment.CreatedAt,
            shipment.Stops.OrderBy(s => s.Sequence).Select(s => s.ToDto()).ToList(),
            shipment.Lines.Select(l => l.ToDto()).ToList(),
            shipment.Returns.Select(r => r.ToDto()).ToList());

    public static ShipmentStopDto ToDto(this ShipmentStop stop)
        => new(
            stop.Id,
            stop.StoreId,
            stop.Sequence,
            stop.Window,
            stop.Status.ToString(),
            stop.ProofOfDelivery?.ToDto());

    public static ProofOfDeliveryDto ToDto(this ProofOfDelivery pod)
        => new(pod.Id, pod.SignedQtyJson, pod.GetPhotoIds(), pod.SignerName, pod.Geo, pod.SignedAt);

    public static ShipmentLineDto ToDto(this ShipmentLine line)
        => new(
            line.Id,
            line.OrderId,
            line.StoreId,
            line.ToteId,
            line.Lots.Select(l => new ShipmentLineLotDto(
                l.OrderLineId, l.ProductId, l.Zone, l.LotId, l.LotNo, l.Quantity)).ToList());

    public static ReturnOnTruckDto ToDto(this ReturnOnTruck ret)
        => new(ret.Id, ret.OrderId, ret.ProductId, ret.LotId, ret.Quantity, ret.Reason);
}
