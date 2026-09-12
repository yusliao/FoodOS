using FSH.Modules.Inventory.Contracts.v1.Kpis;
using FSH.Modules.Ops.Contracts.Dtos;
using FSH.Modules.Ops.Contracts.v1.Kpis;
using FSH.Modules.Ordering.Contracts.v1.Kpis;
using Mediator;

namespace FSH.Modules.Ops.Features.v1.Kpis.GetOpsKpis;

public sealed class GetOpsKpisQueryHandler(IMediator mediator)
    : IQueryHandler<GetOpsKpisQuery, OpsKpisDto>
{
    public async ValueTask<OpsKpisDto> Handle(GetOpsKpisQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        DateOnly date = query.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var orders = await mediator.Send(new GetOrderKpiFactsQuery(date), cancellationToken).ConfigureAwait(false);
        var inventory = await mediator.Send(new GetInventoryLossFactsQuery(date), cancellationToken).ConfigureAwait(false);
        return OpsKpiCalculator.Compute(date, orders, inventory);
    }
}
