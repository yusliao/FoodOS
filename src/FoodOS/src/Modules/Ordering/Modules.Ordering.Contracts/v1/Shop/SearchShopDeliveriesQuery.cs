using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Shop;

public sealed record SearchShopDeliveriesQuery(Guid? StoreId = null)
    : IQuery<IReadOnlyList<ShopDeliveryDto>>;
