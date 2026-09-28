using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Stores;

public sealed record UpdateStoreCommand(Guid StoreId, string Name, string Address) : ICommand;
