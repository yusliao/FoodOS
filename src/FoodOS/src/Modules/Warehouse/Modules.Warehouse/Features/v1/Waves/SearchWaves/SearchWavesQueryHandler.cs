using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Framework.Core.Context;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Waves.SearchWaves;

public sealed class SearchWavesQueryHandler(WarehouseDbContext dbContext, ICurrentUser currentUser, IUserPermissionService permissions)
    : IQueryHandler<SearchWavesQuery, IReadOnlyList<WaveDto>>
{
    public async ValueTask<IReadOnlyList<WaveDto>> Handle(SearchWavesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = await dbContext.Waves.AsNoTracking().Where(w => w.WarehouseId == query.WarehouseId)
            .ApplyReadScopeAsync(currentUser, permissions, cancellationToken).ConfigureAwait(false);
        if (query.BusinessDate is { } date)
        {
            q = q.Where(w => w.BusinessDate == date);
        }

        var waves = await q.OrderBy(w => w.Number).ToListAsync(cancellationToken).ConfigureAwait(false);
        return waves.Select(w => w.ToDto()).ToList();
    }
}
