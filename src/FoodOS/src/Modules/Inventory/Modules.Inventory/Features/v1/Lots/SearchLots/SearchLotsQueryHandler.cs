using FSH.Framework.Shared.Persistence;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Lots;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using FSH.Modules.Inventory.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Lots.SearchLots;

public sealed class SearchLotsQueryHandler(InventoryDbContext dbContext)
    : IQueryHandler<SearchLotsQuery, PagedResponse<LotDto>>
{
    public async ValueTask<PagedResponse<LotDto>> Handle(SearchLotsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        int page = query.PageNumber < 1 ? 1 : query.PageNumber;
        int size = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

        IQueryable<Lot> q = dbContext.Lots.AsNoTracking();
        if (query.ProductId is { } productId)
        {
            q = q.Where(l => l.ProductId == productId);
        }

        if (!string.IsNullOrWhiteSpace(query.LotNo))
        {
            string lotNo = query.LotNo.Trim();
            q = q.Where(l => EF.Functions.ILike(l.LotNo, $"%{lotNo}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<LotStatus>(query.Status, ignoreCase: true, out var status))
        {
            q = q.Where(l => l.Status == status);
        }

        if (query.WarehouseId is { } warehouseId)
        {
            q = q.Where(l => dbContext.LotBalances.Any(b => b.LotId == l.Id && b.WarehouseId == warehouseId));
        }

        q = q.OrderBy(l => l.LotNo);
        long total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<LotDto>
        {
            Items = items.Select(l => l.ToDto()).ToList(),
            PageNumber = page,
            PageSize = size,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)size)
        };
    }
}
