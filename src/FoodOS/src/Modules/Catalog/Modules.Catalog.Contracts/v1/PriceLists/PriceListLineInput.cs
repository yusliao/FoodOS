namespace FSH.Modules.Catalog.Contracts.v1.PriceLists;

public sealed record PriceListLineInput(
    Guid ProductId,
    decimal MinQty,
    decimal UnitPrice,
    string Currency = "USD");
