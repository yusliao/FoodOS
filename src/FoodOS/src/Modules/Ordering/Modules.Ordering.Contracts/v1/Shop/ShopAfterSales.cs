using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Shop;

public sealed record SearchShopAfterSalesQuery(Guid? StoreId = null, Guid? OrderId = null)
    : IQuery<IReadOnlyList<ShopAfterSalesTicketDto>>;

public sealed record CreateShopAfterSalesCommand(
    Guid OrderId,
    Guid OrderLineId,
    string Type,
    decimal Quantity,
    string Reason) : ICommand<ShopAfterSalesTicketDto>;
