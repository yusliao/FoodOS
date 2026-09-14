using FSH.Framework.Shared.Persistence;
using FSH.Modules.Inventory.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Lots;

public sealed record SearchLotsQuery(
    Guid? WarehouseId = null,
    Guid? ProductId = null,
    string? LotNo = null,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<LotDto>>;
