using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Stores;

public sealed record StoreContactInput(string Name, string Phone);

/// <summary>Replaces the store's caller contacts (name + phone) used to recognise phone orders.</summary>
public sealed record ReplaceStoreContactsCommand(Guid StoreId, IReadOnlyList<StoreContactInput> Contacts)
    : ICommand<IReadOnlyList<StoreContactDto>>;

public sealed record GetStoreContactsQuery(Guid StoreId) : IQuery<IReadOnlyList<StoreContactDto>>;
