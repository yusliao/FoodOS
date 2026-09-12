using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Lots;

public sealed record GetLotIsolationQuery(Guid LotId) : IQuery<bool>;
