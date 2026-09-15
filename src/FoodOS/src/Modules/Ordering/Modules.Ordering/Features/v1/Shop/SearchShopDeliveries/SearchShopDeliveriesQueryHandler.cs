using FSH.Framework.Core.Exceptions;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using Mediator;

namespace FSH.Modules.Ordering.Features.v1.Shop.SearchShopDeliveries;

public sealed class SearchShopDeliveriesQueryHandler(
    ICustomerAccessScopeResolver accessScopeResolver,
    IMediator mediator)
    : IQueryHandler<SearchShopDeliveriesQuery, IReadOnlyList<ShopDeliveryDto>>
{
    public async ValueTask<IReadOnlyList<ShopDeliveryDto>> Handle(
        SearchShopDeliveriesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var access = await accessScopeResolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (query.StoreId is { } storeId && !access.StoreIds.Contains(storeId))
        {
            throw new NotFoundException($"Store {storeId} not found.");
        }

        IReadOnlyList<Guid> storeIds = query.StoreId is { } selectedStoreId
            ? [selectedStoreId]
            : access.StoreIds;
        var deliveries = await mediator.Send(new GetCustomerDeliveriesQuery(storeIds), cancellationToken)
            .ConfigureAwait(false);
        return deliveries.Select(item => new ShopDeliveryDto(
            item.ShipmentId,
            item.ShipmentNumber,
            item.StoreId,
            item.BusinessDate,
            item.ShipmentStatus,
            item.StopStatus,
            item.Sequence,
            item.DeliveryWindow,
            item.SignedAt,
            item.OrderIds)).ToList();
    }
}
