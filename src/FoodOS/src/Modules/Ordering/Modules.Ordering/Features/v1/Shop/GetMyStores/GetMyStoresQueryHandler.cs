using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Shop.GetMyStores;

public sealed class GetMyStoresQueryHandler(
    OrderingDbContext dbContext,
    ICustomerAccessScopeResolver accessScopeResolver)
    : IQueryHandler<GetMyStoresQuery, IReadOnlyList<ShopStoreDto>>,
      IQueryHandler<GetMyStoreByIdQuery, ShopStoreDto>
{
    public async ValueTask<IReadOnlyList<ShopStoreDto>> Handle(
        GetMyStoresQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var access = await accessScopeResolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        return await dbContext.Stores
            .AsNoTracking()
            .Where(store =>
                store.CustomerTenantId == access.CustomerTenantId
                && store.CustomerOrgId == access.CustomerOrgId
                && access.StoreIds.Contains(store.Id))
            .OrderBy(store => store.Name)
            .ThenBy(store => store.Id)
            .Select(store => new ShopStoreDto(
                store.Id,
                store.Code,
                store.Name,
                store.Address,
                store.DeliveryWindow))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<ShopStoreDto> Handle(
        GetMyStoreByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var access = await accessScopeResolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
        return await dbContext.Stores
            .AsNoTracking()
            .Where(store =>
                store.Id == query.StoreId
                && store.CustomerTenantId == access.CustomerTenantId
                && store.CustomerOrgId == access.CustomerOrgId
                && access.StoreIds.Contains(store.Id))
            .Select(store => new ShopStoreDto(
                store.Id,
                store.Code,
                store.Name,
                store.Address,
                store.DeliveryWindow))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Store {query.StoreId} not found.");
    }
}
