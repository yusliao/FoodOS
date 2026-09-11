namespace FSH.Modules.Procurement.Contracts.Dtos;

public sealed record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    string? Categories,
    int LeadDays,
    string Status,
    DateTime CreatedAtUtc);

public sealed record PurchaseOrderLineDto(
    Guid Id,
    Guid ProductId,
    string Zone,
    decimal Quantity,
    decimal ReceivedQty,
    decimal RejectedQty);

public sealed record InboundAppointmentDto(
    Guid Id,
    Guid PurchaseOrderId,
    string DockSlot,
    string? VehicleNo,
    DateTimeOffset CreatedAt);

public sealed record QualityCheckDto(
    Guid Id,
    Guid PurchaseOrderId,
    Guid LineId,
    Guid InspectorUserId,
    string Result,
    decimal SampleQty,
    decimal Quantity,
    string LotNo,
    Guid? LotId,
    string? Note,
    IReadOnlyList<Guid> PhotoFileIds,
    DateTimeOffset CheckedAt);

public sealed record PurchaseOrderDto(
    Guid Id,
    string Number,
    Guid SupplierId,
    Guid WarehouseId,
    string Status,
    DateTimeOffset ExpectedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    InboundAppointmentDto? Appointment,
    IReadOnlyList<QualityCheckDto> QualityChecks);
