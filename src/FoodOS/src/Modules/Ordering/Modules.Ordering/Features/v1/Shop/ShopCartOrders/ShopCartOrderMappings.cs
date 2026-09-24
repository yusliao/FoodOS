using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Domain;

namespace FSH.Modules.Ordering.Features.v1.Shop.ShopCartOrders;

internal static class ShopCartOrderMappings
{
    public static ShopCartDto ToShopDto(this Cart cart)
        => new(
            cart.Id,
            cart.StoreId,
            cart.Lines.Select(line => new ShopCartLineDto(line.ProductId, line.Quantity)).ToList(),
            cart.UpdatedAt);

    public static ShopOrderDto ToShopDto(this SalesOrder order)
        => new(
            order.Id,
            order.Number,
            order.StoreId,
            order.Status.ToString(),
            order.BusinessDate,
            order.CutoffAt,
            order.PlacedAt,
            order.Revision,
            order.WarehouseConfirmationStatus.ToString(),
            order.WarehouseConfirmationDetail,
            order.WarehouseConfirmationUpdatedAt,
            order.Lines.Select(line => new ShopOrderLineDto(
                line.Id,
                line.ProductId,
                line.OrderedQty,
                line.DeliveredQty,
                line.ReturnedQty,
                line.ShortageQty,
                line.ShortageReason,
                line.UnitPrice,
                line.Currency)).ToList());
}
