using FSH.Framework.Shared.Persistence;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Shop;

public sealed record GetShopCartQuery(Guid StoreId) : IQuery<ShopCartDto>;

public sealed record UpdateShopCartCommand(
    Guid StoreId,
    IReadOnlyList<CartLineInput> Lines) : ICommand<Guid>;

public sealed record SearchShopOrdersQuery(
    Guid? StoreId = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<ShopOrderDto>>;

public sealed record GetShopOrderByIdQuery(Guid OrderId) : IQuery<ShopOrderDto>;

public sealed record PlaceShopOrderCommand(Guid StoreId) : ICommand<Guid>;

public sealed record AmendShopOrderCommand(
    Guid OrderId,
    IReadOnlyList<AmendOrderLineInput> Lines) : ICommand<Guid>;

public sealed record CancelShopOrderCommand(Guid OrderId) : ICommand<Guid>;
