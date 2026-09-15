using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.StoreAccess;

public sealed record SetUserStoreAccessCommand(
    Guid UserId,
    IReadOnlyList<Guid> StoreIds) : ICommand;
