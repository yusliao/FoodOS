namespace FSH.Modules.Ordering.Contracts.Dtos;

/// <summary>
/// Order quantities for the operations board. No HTTP; Ops mediates.
/// </summary>
public sealed record OrderKpiFactsDto(
    int CommittedOrderCount,
    int FulfilledOrderCount,
    decimal OrderedQty,
    decimal ReservedQty,
    decimal DeliveredQty);
