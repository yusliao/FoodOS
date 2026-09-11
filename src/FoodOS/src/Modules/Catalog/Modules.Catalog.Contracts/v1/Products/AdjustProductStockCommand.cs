using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.Products;

/// <summary>
/// Deprecated. Catalog <c>Product.Stock</c> is not operational inventory.
/// The HTTP endpoint returns 410 Gone; available quantity lives in Inventory.
/// </summary>
public sealed record AdjustProductStockCommand(
    Guid ProductId,
    int Delta) : ICommand<int>;
