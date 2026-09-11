using FSH.Modules.Catalog.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.PriceLists;

public sealed record QuoteProductPriceQuery(
    Guid CustomerOrgId,
    Guid ProductId,
    decimal Quantity,
    DateTimeOffset? AsOf = null) : IQuery<PriceQuoteDto>;
