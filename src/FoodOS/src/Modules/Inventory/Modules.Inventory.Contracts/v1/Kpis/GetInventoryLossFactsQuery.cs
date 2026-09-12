using FSH.Modules.Inventory.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Kpis;

public sealed record GetInventoryLossFactsQuery(DateOnly Date) : IQuery<InventoryLossFactsDto>;
