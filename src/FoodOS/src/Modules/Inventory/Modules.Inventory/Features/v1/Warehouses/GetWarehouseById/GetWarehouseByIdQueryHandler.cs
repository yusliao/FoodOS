using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Features.v1.Warehouses;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.GetWarehouseById;

public sealed class GetWarehouseByIdQueryHandler(InventoryDbContext dbContext)
    : IQueryHandler<GetWarehouseByIdQuery, WarehouseDto>
{
    public async ValueTask<WarehouseDto> Handle(GetWarehouseByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var warehouse = await dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == query.WarehouseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Warehouse {query.WarehouseId} not found.");

        return warehouse.ToDto();
    }
}
