using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

public sealed record UnreserveStockCommand(
    Guid ReservationId,
    string IdempotencyKey) : ICommand<Guid>;
