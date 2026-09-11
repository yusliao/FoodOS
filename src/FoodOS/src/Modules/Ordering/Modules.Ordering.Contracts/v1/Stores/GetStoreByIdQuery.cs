using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Stores;

public sealed record GetStoreByIdQuery(Guid StoreId) : IQuery<StoreDto>;
