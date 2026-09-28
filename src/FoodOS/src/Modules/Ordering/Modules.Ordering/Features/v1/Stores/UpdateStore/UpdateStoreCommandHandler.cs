using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Stores.UpdateStore;

public sealed class UpdateStoreCommandHandler(OrderingDbContext dbContext)
    : ICommandHandler<UpdateStoreCommand>
{
    public async ValueTask<Unit> Handle(UpdateStoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var store = await dbContext.Stores
            .SingleOrDefaultAsync(item => item.Id == command.StoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Store {command.StoreId} not found.");

        store.UpdateDetails(command.Name, command.Address);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
