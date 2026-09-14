using FSH.Framework.Shared.Persistence;
using FSH.Modules.Inventory.Contracts.Dtos;
using Mediator;
using FSH.Modules.Inventory.Contracts;

namespace FSH.Modules.Inventory.Contracts.v1.Stock;

public sealed record SearchInventoryTransactionsQuery(
    Guid WarehouseId,
    Guid? LotId = null,
    Guid? ProductId = null,
    TemperatureZoneKind? Zone = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<InventoryTransactionDto>>;
