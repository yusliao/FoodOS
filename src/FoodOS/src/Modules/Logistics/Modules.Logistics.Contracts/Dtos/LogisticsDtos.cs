namespace FSH.Modules.Logistics.Contracts.Dtos;

public sealed record VehicleDto(
    Guid Id,
    string Plate,
    string CompartmentZones,
    decimal PayloadKg);

public sealed record DriverDto(
    Guid Id,
    Guid UserId,
    string Phone);

public sealed record RouteDto(
    Guid Id,
    Guid WarehouseId,
    string Code,
    IReadOnlyList<Guid> StoreIds,
    Guid? DefaultVehicleId);

public sealed record ShipmentLineLotDto(
    Guid OrderLineId,
    Guid ProductId,
    string Zone,
    Guid LotId,
    string LotNo,
    decimal Quantity);

public sealed record ShipmentLineDto(
    Guid Id,
    Guid OrderId,
    Guid StoreId,
    Guid? ToteId,
    IReadOnlyList<ShipmentLineLotDto> Lots);

public sealed record ReturnOnTruckDto(
    Guid Id,
    Guid OrderId,
    Guid ProductId,
    Guid LotId,
    decimal Quantity,
    string Reason);

public sealed record ProofOfDeliveryDto(
    Guid Id,
    string SignedQtyJson,
    IReadOnlyList<Guid> PhotoFileIds,
    string SignerName,
    string? Geo,
    DateTimeOffset SignedAt);

public sealed record ShipmentStopDto(
    Guid Id,
    Guid StoreId,
    int Sequence,
    string? Window,
    string Status,
    ProofOfDeliveryDto? ProofOfDelivery);

public sealed record ShipmentDto(
    Guid Id,
    string Number,
    Guid RouteId,
    Guid WarehouseId,
    DateOnly BusinessDate,
    Guid VehicleId,
    Guid DriverId,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ShipmentStopDto> Stops,
    IReadOnlyList<ShipmentLineDto> Lines,
    IReadOnlyList<ReturnOnTruckDto> Returns);
