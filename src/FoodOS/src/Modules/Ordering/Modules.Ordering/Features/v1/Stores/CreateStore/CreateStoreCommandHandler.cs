using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Stores.CreateStore;

public sealed class CreateStoreCommandHandler(OrderingDbContext dbContext, IMediator mediator)
    : ICommandHandler<CreateStoreCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateStoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var org = await dbContext.CustomerOrgs
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == command.CustomerOrgId, cancellationToken)
            .ConfigureAwait(false);
        if (org is null)
        {
            throw new NotFoundException($"Customer organization {command.CustomerOrgId} not found.");
        }

        _ = await mediator.Send(new GetWarehouseByIdQuery(command.DefaultWarehouseId), cancellationToken)
            .ConfigureAwait(false);

        var store = Store.Create(
            command.CustomerOrgId,
            command.Code,
            command.Name,
            command.Address,
            command.DefaultWarehouseId,
            command.DefaultRouteId,
            command.DeliveryWindow,
            org.CustomerTenantId);

        bool taken = await dbContext.Stores
            .AnyAsync(s => s.Code == store.Code, cancellationToken)
            .ConfigureAwait(false);
        if (taken)
        {
            throw new CustomException(
                $"A store with code '{store.Code}' already exists.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        dbContext.Stores.Add(store);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return store.Id;
    }
}
