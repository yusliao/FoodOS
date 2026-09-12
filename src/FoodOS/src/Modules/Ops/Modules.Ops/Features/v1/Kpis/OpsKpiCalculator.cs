using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Ops.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.Dtos;

namespace FSH.Modules.Ops.Features.v1.Kpis;

internal static class OpsKpiCalculator
{
    public static OpsKpisDto Compute(DateOnly date, OrderKpiFactsDto orders, InventoryLossFactsDto inventory)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(inventory);

        decimal shortQty = orders.OrderedQty - orders.ReservedQty;
        if (shortQty < 0)
        {
            shortQty = 0;
        }

        return new OpsKpisDto(
            date,
            Ratio(orders.FulfilledOrderCount, orders.CommittedOrderCount),
            Ratio(shortQty, orders.OrderedQty),
            ShrinkageRate(inventory.LossQty, inventory.InboundQty),
            TemperatureComplianceRate: null,
            orders.CommittedOrderCount,
            orders.FulfilledOrderCount,
            orders.OrderedQty,
            inventory.InboundQty,
            inventory.LossQty);
    }

    internal static decimal Ratio(decimal numerator, decimal denominator)
    {
        if (denominator <= 0)
        {
            return 0m;
        }

        return decimal.Round(numerator / denominator, 4, MidpointRounding.AwayFromZero);
    }

    internal static decimal ShrinkageRate(decimal lossQty, decimal inboundQty)
    {
        if (lossQty <= 0 && inboundQty <= 0)
        {
            return 0m;
        }

        if (inboundQty <= 0)
        {
            return 1m;
        }

        decimal rate = lossQty / inboundQty;
        if (rate > 1m)
        {
            rate = 1m;
        }

        return decimal.Round(rate, 4, MidpointRounding.AwayFromZero);
    }
}
