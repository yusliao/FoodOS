using FSH.Framework.Shared.Persistence;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Inventory.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock.SearchInventoryTransactions;

public sealed class SearchInventoryTransactionsQueryHandler(InventoryDbContext dbContext)
    : IQueryHandler<SearchInventoryTransactionsQuery, PagedResponse<InventoryTransactionDto>>
{
    public async ValueTask<PagedResponse<InventoryTransactionDto>> Handle(
        SearchInventoryTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        int page = query.PageNumber < 1 ? 1 : query.PageNumber;
        int size = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

        IQueryable<InventoryTransaction> q = dbContext.InventoryTransactions.AsNoTracking()
            .Where(t => t.WarehouseId == query.WarehouseId);

        if (query.LotId is { } lotId)
        {
            q = q.Where(t => t.LotId == lotId);
        }

        if (query.ProductId is { } productId)
        {
            q = q.Where(t => t.ProductId == productId);
        }

        if (query.Zone is { } zoneKind)
        {
            var zoneId = await dbContext.TemperatureZones
                .AsNoTracking()
                .Where(z => z.WarehouseId == query.WarehouseId && z.Kind == zoneKind)
                .Select(z => (Guid?)z.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (zoneId is null)
            {
                return new PagedResponse<InventoryTransactionDto>
                {
                    Items = [],
                    PageNumber = page,
                    PageSize = size,
                    TotalCount = 0,
                    TotalPages = 0
                };
            }

            q = q.Where(t => t.ZoneId == zoneId.Value);
        }

        q = q.OrderByDescending(t => t.OccurredAt);
        long total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<InventoryTransactionDto>
        {
            Items = items.Select(t => t.ToDto()).ToList(),
            PageNumber = page,
            PageSize = size,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)size)
        };
    }
}
