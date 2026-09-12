using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Kpis;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Kpis.GetInventoryLossFacts;

public sealed class GetInventoryLossFactsQueryHandler(InventoryDbContext dbContext)
    : IQueryHandler<GetInventoryLossFactsQuery, InventoryLossFactsDto>
{
    public async ValueTask<InventoryLossFactsDto> Handle(
        GetInventoryLossFactsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var start = new DateTimeOffset(query.Date, TimeOnly.MinValue, TimeSpan.Zero);
        var end = start.AddDays(1);

        var rows = await dbContext.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.OccurredAt >= start && t.OccurredAt < end)
            .Where(t =>
                t.Type == InventoryTransactionType.Receive
                || t.Type == InventoryTransactionType.Isolate
                || t.Type == InventoryTransactionType.AdjustShrink)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Qty = g.Sum(t => t.Quantity) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal inbound = rows.Where(r => r.Type == InventoryTransactionType.Receive).Sum(r => r.Qty);
        decimal loss = rows
            .Where(r =>
                r.Type == InventoryTransactionType.Isolate
                || r.Type == InventoryTransactionType.AdjustShrink)
            .Sum(r => r.Qty);

        return new InventoryLossFactsDto(inbound, loss);
    }
}
