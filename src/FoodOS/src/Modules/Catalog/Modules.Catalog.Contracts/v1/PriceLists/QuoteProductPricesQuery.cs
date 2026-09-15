using FSH.Modules.Catalog.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.PriceLists;

public sealed record ProductPriceRequestDto(Guid ProductId, decimal Quantity);

public sealed record QuoteProductPricesQuery(
    Guid CustomerOrgId,
    IReadOnlyList<ProductPriceRequestDto> Products,
    DateTimeOffset? AsOf = null) : IQuery<IReadOnlyList<PriceQuoteDto>>;
