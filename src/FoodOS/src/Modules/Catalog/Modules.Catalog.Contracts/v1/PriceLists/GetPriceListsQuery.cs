using FSH.Modules.Catalog.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.PriceLists;

public sealed record GetPriceListsQuery(Guid? CustomerOrgId = null) : IQuery<IReadOnlyList<PriceListDto>>;
