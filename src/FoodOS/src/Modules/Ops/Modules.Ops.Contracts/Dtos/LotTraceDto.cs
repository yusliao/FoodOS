namespace FSH.Modules.Ops.Contracts.Dtos;

/// <summary>
/// Cross-module lot timeline. P0 has receiving, picking, shipping, arriving;
/// storing (putaway) is not written until Warehouse putaway exists.
/// </summary>
public sealed record LotTraceDto(
    Guid LotId,
    IReadOnlyList<LotTracePointDto> Events);

public sealed record LotTracePointDto(
    Guid Id,
    string Module,
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
