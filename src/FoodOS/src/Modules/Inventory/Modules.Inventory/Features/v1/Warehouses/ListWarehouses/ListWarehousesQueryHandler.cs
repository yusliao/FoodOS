using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Features.v1.Warehouses;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.ListWarehouses;

public sealed class ListWarehousesQueryHandler(InventoryDbContext dbContext)
    : IQueryHandler<ListWarehousesQuery, IReadOnlyList<WarehouseDto>>
{
    public async ValueTask<IReadOnlyList<WarehouseDto>> Handle(
        ListWarehousesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var warehouses = await dbContext.Warehouses
            .AsNoTracking()
            .OrderBy(w => w.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return warehouses.Select(w => w.ToDto()).ToList();
    }
}
