namespace FSH.Modules.Warehouse.Contracts.Dtos;

/// <summary>
/// One EPCIS-style node. No HTTP; Ops mediates a cross-module timeline.
/// </summary>
public sealed record LotTraceEventDto(
    Guid Id,
    Guid LotId,
    Guid ProductId,
    string BizStep,
    string Disposition,
    decimal Quantity,
    string Uom,
    string? SourceLocation,
    string? DestLocation,
    string ActorUserId,
    DateTimeOffset OccurredAt,
    string RefType,
    Guid RefId,
    string? EvidenceUrl);
