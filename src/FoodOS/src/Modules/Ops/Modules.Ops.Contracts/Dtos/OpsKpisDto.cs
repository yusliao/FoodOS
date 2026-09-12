namespace FSH.Modules.Ops.Contracts.Dtos;

/// <summary>
/// Daily operations board. Rates are 0–1. Temperature is null when no MQTT samples exist (P0).
/// </summary>
public sealed record OpsKpisDto(
    DateOnly Date,
    decimal FulfillmentRate,
    decimal StockoutRate,
    decimal ShrinkageRate,
    decimal? TemperatureComplianceRate,
    int CommittedOrderCount,
    int FulfilledOrderCount,
    decimal OrderedQty,
    decimal InboundQty,
    decimal LossQty);
