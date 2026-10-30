using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.PriceLists;

/// <summary>Removes one quantity-tier line from a price list. Orders keep their snapshotted prices.</summary>
public sealed record DeletePriceListLineCommand(Guid PriceListId, Guid LineId) : ICommand<Unit>;
