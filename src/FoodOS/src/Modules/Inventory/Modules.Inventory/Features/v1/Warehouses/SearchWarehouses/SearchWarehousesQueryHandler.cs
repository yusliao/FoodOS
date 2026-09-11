using FSH.Framework.Shared.Persistence;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Inventory.Features.v1.Warehouses;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.SearchWarehouses;

public sealed class SearchWarehousesQueryHandler(InventoryDbContext dbContext)
    : IQueryHandler<SearchWarehousesQuery, PagedResponse<WarehouseDto>>
{
    public async ValueTask<PagedResponse<WarehouseDto>> Handle(SearchWarehousesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        int page = query.PageNumber < 1 ? 1 : query.PageNumber;
        int size = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

        IQueryable<Warehouse> q = dbContext.Warehouses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            q = q.Where(w =>
                EF.Functions.ILike(w.Code, $"%{term}%") ||
                EF.Functions.ILike(w.Name, $"%{term}%") ||
                EF.Functions.ILike(w.City, $"%{term}%"));
        }

        q = q.OrderBy(w => w.Code);
        long total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<WarehouseDto>
        {
            Items = items.Select(w => w.ToDto()).ToList(),
            PageNumber = page,
            PageSize = size,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)size)
        };
    }
}
