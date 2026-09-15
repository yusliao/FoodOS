using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Shop;

public sealed record GetMyStoresQuery : IQuery<IReadOnlyList<ShopStoreDto>>;

public sealed record GetMyStoreByIdQuery(Guid StoreId) : IQuery<ShopStoreDto>;
