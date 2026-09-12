using FSH.Modules.Inventory.Contracts.v1.Lots;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Lots.GetLotIsolation;

public sealed class GetLotIsolationQueryHandler(InventoryDbContext dbContext)
    : IQueryHandler<GetLotIsolationQuery, bool>
{
    public async ValueTask<bool> Handle(GetLotIsolationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var lot = await dbContext.Lots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == query.LotId, cancellationToken)
            .ConfigureAwait(false);

        return lot is not null && lot.Status == LotStatus.Isolated;
    }
}
