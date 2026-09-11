using FSH.Modules.Logistics.Contracts.v1.Routes;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Routes.CreateRoute;

public sealed class CreateRouteCommandHandler(LogisticsDbContext dbContext)
    : ICommandHandler<CreateRouteCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateRouteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        string code = command.Code.Trim().ToUpperInvariant();
        var existing = await dbContext.Routes
            .FirstOrDefaultAsync(
                r => r.WarehouseId == command.WarehouseId && r.Code == code,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Id;
        }

        var route = Route.Create(command.WarehouseId, code, command.StoreIds, command.DefaultVehicleId);
        dbContext.Routes.Add(route);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return route.Id;
    }
}
