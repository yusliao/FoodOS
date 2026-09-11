using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.PriceLists;

public sealed record UpsertProductPriceLockCommand(
    Guid CustomerOrgId,
    Guid ProductId,
    decimal UnitPrice,
    DateTimeOffset Until,
    string Currency = "USD") : ICommand<Guid>;
