using FSH.Modules.Inventory.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

/// <summary>
/// Convert a SKU-level reservation into lot Allocated buckets using FEFO.
/// Isolated lots are skipped. Shortfall is returned as <see cref="AllocateReservationResult.ShortageQty"/>.
/// </summary>
public sealed record AllocateReservationCommand(
    Guid ReservationId,
    string IdempotencyKey,
    int MinRemainingDaysOnShip = 0) : ICommand<AllocateReservationResult>;
