using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Stores.StoreContacts;

public sealed class StoreContactsHandler(OrderingDbContext dbContext)
    : IQueryHandler<GetStoreContactsQuery, IReadOnlyList<StoreContactDto>>,
      ICommandHandler<ReplaceStoreContactsCommand, IReadOnlyList<StoreContactDto>>
{
    public async ValueTask<IReadOnlyList<StoreContactDto>> Handle(
        GetStoreContactsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var store = await dbContext.Stores.AsNoTracking()
            .Include(item => item.Contacts)
            .SingleOrDefaultAsync(item => item.Id == query.StoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Store {query.StoreId} not found.");
        return ToDtos(store.Contacts);
    }

    public async ValueTask<IReadOnlyList<StoreContactDto>> Handle(
        ReplaceStoreContactsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var store = await dbContext.Stores
            .Include(item => item.Contacts)
            .SingleOrDefaultAsync(item => item.Id == command.StoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Store {command.StoreId} not found.");
        var contacts = store.ReplaceContacts(
            command.Contacts.Select(contact => (contact.Name, contact.Phone)).ToList());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDtos(contacts);
    }

    internal static IReadOnlyList<StoreContactDto> ToDtos(IEnumerable<StoreContact> contacts) =>
        contacts
            .OrderBy(contact => contact.Name, StringComparer.CurrentCulture)
            .Select(contact => new StoreContactDto(contact.Id, contact.Name, contact.Phone))
            .ToList();
}
