using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.PriceLists;

public sealed record UpsertPriceListLineCommand(
    Guid PriceListId,
    Guid ProductId,
    decimal MinQty,
    decimal UnitPrice,
    string Currency = "USD") : ICommand<Guid>;
