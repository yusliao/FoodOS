using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Ops.Features.v1.Kpis;
using FSH.Modules.Ordering.Contracts.Dtos;

namespace Ops.Tests.Kpis;

public sealed class OpsKpiCalculatorTests
{
    [Fact]
    public void EmptyDay_Should_ReturnZeroRates_AndNullTemperature()
    {
        var date = new DateOnly(2026, 9, 12);
        var dto = OpsKpiCalculator.Compute(
            date,
            new OrderKpiFactsDto(0, 0, 0, 0, 0),
            new InventoryLossFactsDto(0, 0));

        dto.Date.ShouldBe(date);
        dto.FulfillmentRate.ShouldBe(0m);
        dto.StockoutRate.ShouldBe(0m);
        dto.ShrinkageRate.ShouldBe(0m);
        dto.TemperatureComplianceRate.ShouldBeNull();
    }

    [Fact]
    public void Fulfillment_Should_BeReceivedShareOfCommittedOrders()
    {
        var dto = OpsKpiCalculator.Compute(
            new DateOnly(2026, 9, 12),
            new OrderKpiFactsDto(CommittedOrderCount: 4, FulfilledOrderCount: 3, OrderedQty: 10, ReservedQty: 10, DeliveredQty: 8),
            new InventoryLossFactsDto(10, 0));

        dto.FulfillmentRate.ShouldBe(0.75m);
        dto.StockoutRate.ShouldBe(0m);
    }

    [Fact]
    public void Stockout_Should_UseUnreservedShareOfOrderedQty()
    {
        var dto = OpsKpiCalculator.Compute(
            new DateOnly(2026, 9, 12),
            new OrderKpiFactsDto(2, 0, OrderedQty: 10, ReservedQty: 7, DeliveredQty: 0),
            new InventoryLossFactsDto(0, 0));

        dto.StockoutRate.ShouldBe(0.3m);
    }

    [Fact]
    public void Shrinkage_Should_BeIsolateShareOfReceive_CappedAtOne()
    {
        OpsKpiCalculator.ShrinkageRate(2, 10).ShouldBe(0.2m);
        OpsKpiCalculator.ShrinkageRate(12, 10).ShouldBe(1m);
        OpsKpiCalculator.ShrinkageRate(4, 0).ShouldBe(1m);
        OpsKpiCalculator.ShrinkageRate(0, 0).ShouldBe(0m);
    }
}
