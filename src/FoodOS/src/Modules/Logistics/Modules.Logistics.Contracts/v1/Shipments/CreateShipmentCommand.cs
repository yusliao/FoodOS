using FSH.Modules.Logistics.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Logistics.Contracts.v1.Shipments;

public sealed record CreateShipmentCommand(
    Guid RouteId,
    Guid WarehouseId,
    Guid VehicleId,
    Guid DriverId,
    DateOnly? BusinessDate = null) : ICommand<ShipmentDto>;
