using FSH.Modules.Catalog.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.PriceLists;

public sealed record GetPriceListsQuery(Guid? CustomerOrgId = null, Guid? CustomerLevelId = null)
    : IQuery<IReadOnlyList<PriceListDto>>;
