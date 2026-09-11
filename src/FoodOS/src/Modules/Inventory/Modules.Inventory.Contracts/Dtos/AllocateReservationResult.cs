namespace FSH.Modules.Inventory.Contracts.Dtos;

public sealed record AllocateReservationResult(
    Guid ReservationId,
    IReadOnlyList<LotAllocationDto> Allocations,
    decimal ShortageQty);
