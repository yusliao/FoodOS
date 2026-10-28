using FSH.Modules.Catalog.Domain;

namespace Catalog.Tests.Domain;

public sealed class CustomerLevelTests
{
    [Fact]
    public void Create_Should_NormalizeCode_And_TrimName()
    {
        var level = CustomerLevel.Create("  vip ", " VIP 客户 ", 10m);

        level.Code.ShouldBe("VIP");
        level.Name.ShouldBe("VIP 客户");
        level.DiscountPercent.ShouldBe(10m);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void Create_Should_RejectDiscountOutsideRange(decimal discount)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CustomerLevel.Create("A", "A", discount));
    }

    [Fact]
    public void ApplyDiscount_Should_KeepPrice_When_NoDiscount()
    {
        CustomerLevel.Create("STD", "Standard", 0m).ApplyDiscount(9.999m).ShouldBe(9.999m);
    }

    [Fact]
    public void PriceList_Should_RejectCustomerAndLevelTogether()
    {
        Should.Throw<ArgumentException>(() => PriceList.Create(
            "both", Guid.CreateVersion7(), DateTimeOffset.UtcNow, customerLevelId: Guid.CreateVersion7()));
    }

    [Fact]
    public void Assignment_Should_MoveToAnotherLevel()
    {
        var assignment = CustomerLevelAssignment.Create(Guid.CreateVersion7(), Guid.CreateVersion7());
        var next = Guid.CreateVersion7();

        assignment.MoveTo(next);

        assignment.CustomerLevelId.ShouldBe(next);
    }
}
