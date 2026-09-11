using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Features.v1.Stock;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.GetAvailableQty;

public sealed class GetAvailableQtyQueryHandler(InventoryDbContext dbContext, TimeProvider clock)
    : IQueryHandler<GetAvailableQtyQuery, AvailableQtyDto>
{
    public async ValueTask<AvailableQtyDto> Handle(GetAvailableQtyQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        Guid? zoneId = null;
        if (query.Zone is { } zoneKind)
        {
            zoneId = await dbContext.TemperatureZones.AsNoTracking()
                .Where(z => z.WarehouseId == query.WarehouseId && z.Kind == zoneKind)
                .Select(z => (Guid?)z.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (zoneId is null)
            {
                return new AvailableQtyDto(query.WarehouseId, query.ProductId, 0m, zoneKind.ToString());
            }
        }

        DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        decimal available = await StockAvailability.ComputeAsync(
                dbContext,
                query.WarehouseId,
                query.ProductId,
                zoneId,
                today,
                cancellationToken)
            .ConfigureAwait(false);

        return new AvailableQtyDto(
            query.WarehouseId,
            query.ProductId,
            available,
            query.Zone?.ToString() ?? "All");
    }
}
