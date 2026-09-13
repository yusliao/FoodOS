using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Domain;

namespace FSH.Modules.Ordering.Features.v1;

internal static class OrderingMappings
{
    public static CustomerOrgDto ToDto(this CustomerOrg org)
        => new(org.Id, org.Code, org.Name, org.CreditHold, org.CreatedAtUtc);

    public static StoreDto ToDto(this Store store)
        => new(
            store.Id,
            store.CustomerOrgId,
            store.Code,
            store.Name,
            store.Address,
            store.DefaultWarehouseId,
            store.DefaultRouteId,
            store.DeliveryWindow,
            store.CreatedAtUtc);

    public static CartDto ToDto(this Cart cart)
        => new(
            cart.Id,
            cart.StoreId,
            cart.Lines.Select(l => new CartLineDto(l.Id, l.ProductId, l.Quantity, l.Zone)).ToList(),
            cart.UpdatedAt);

    public static SalesOrderDto ToDto(this SalesOrder order)
        => new(
            order.Id,
            order.Number,
            order.StoreId,
            order.CustomerOrgId,
            order.WarehouseId,
            order.Status.ToString(),
            order.BusinessDate,
            order.CutoffAt,
            order.PlacedAt,
            order.Revision,
            order.Lines.Select(l => new SalesOrderLineDto(
                l.Id,
                l.ProductId,
                l.Zone,
                l.OrderedQty,
                l.ReservedQty,
                l.DeliveredQty,
                l.ReturnedQty,
                l.ShortageQty,
                l.ShortageReason,
                l.VarianceReason,
                l.UnitPrice,
                l.Currency,
                l.ReservationId,
                l.Lots.Select(lot => new SalesOrderLineLotDto(
                    lot.LotId,
                    lot.LotNo,
                    lot.ShippedQty,
                    lot.DeliveredQty,
                    lot.ReturnedQty)).ToList())).ToList());

    public static AfterSalesTicketDto ToDto(this AfterSalesTicket ticket)
        => new(
            ticket.Id,
            ticket.OrderId,
            ticket.StoreId,
            ticket.OrderLineId,
            ticket.Type.ToString(),
            ticket.Quantity,
            ticket.Reason,
            ticket.Status.ToString(),
            ticket.CreatedByUserId,
            ticket.CreatedAt);

    public static CartDto EmptyCart(Guid storeId)
        => new(Guid.Empty, storeId, [], DateTimeOffset.MinValue);
}
