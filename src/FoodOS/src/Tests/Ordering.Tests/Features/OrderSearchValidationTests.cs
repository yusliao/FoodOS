using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using FSH.Modules.Ordering.Features.v1.Orders.SearchOrders;
using FSH.Modules.Ordering.Features.v1.Shop.ShopCartOrders;

namespace Ordering.Tests.Features;

public sealed class OrderSearchValidationTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("Received", true)]
    [InlineData("received", true)]
    [InlineData("Reconciled", true)]
    [InlineData("InTransit", true)]
    [InlineData("Cancelled", true)]
    [InlineData("Paid", false)]
    [InlineData("7", false)]
    [InlineData("999", false)]
    [InlineData("", false)]
    [InlineData("Received,Reconciled", false)]
    public void Status_Should_Use_Existing_Order_State_Names(string? status, bool valid)
    {
        new SearchOrdersQueryValidator().Validate(new SearchOrdersQuery(Status: status)).IsValid.ShouldBe(valid);
        new SearchShopOrdersQueryValidator().Validate(new SearchShopOrdersQuery(Status: status)).IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(1, -1)]
    [InlineData(1, 201)]
    public void Invalid_Pagination_Should_Remain_Rejected(int page, int size)
    {
        new SearchOrdersQueryValidator().Validate(new SearchOrdersQuery(PageNumber: page, PageSize: size))
            .IsValid.ShouldBeFalse();
        new SearchShopOrdersQueryValidator().Validate(new SearchShopOrdersQuery(PageNumber: page, PageSize: size))
            .IsValid.ShouldBeFalse();
    }
}
