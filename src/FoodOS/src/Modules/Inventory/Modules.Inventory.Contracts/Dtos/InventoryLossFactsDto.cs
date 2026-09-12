namespace FSH.Modules.Inventory.Contracts.Dtos;

/// <summary>
/// Inbound vs isolate/shrink quantities for the operations board. No HTTP; Ops mediates.
/// </summary>
public sealed record InventoryLossFactsDto(
    decimal InboundQty,
    decimal LossQty);
