using Mediator;

namespace FSH.Modules.Warehouse.Contracts.v1.Pack;

public sealed record ListOrdersForTotesQuery(IReadOnlyList<Guid> ToteIds) : IQuery<IReadOnlyList<Guid>>;
