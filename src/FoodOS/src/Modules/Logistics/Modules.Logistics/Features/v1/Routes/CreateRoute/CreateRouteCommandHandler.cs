using FSH.Modules.Logistics.Contracts.v1.Routes;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Ordering.Contracts.v1.Stores;
using System.Net;

namespace FSH.Modules.Logistics.Features.v1.Routes.CreateRoute;

public sealed class CreateRouteCommandHandler(LogisticsDbContext dbContext, IMediator mediator,
    IMultiTenantContextAccessor<AppTenantInfo> tenantContext)
    : ICommandHandler<CreateRouteCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateRouteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (tenantContext.MultiTenantContext.TenantInfo?.Id != MultitenancyConstants.Root.Id)
        {
            throw new CustomException("Route registration requires the operator identity domain.",
                (IEnumerable<string>?)null, HttpStatusCode.Forbidden);
        }
        _ = await mediator.Send(new GetWarehouseByIdQuery(command.WarehouseId), cancellationToken).ConfigureAwait(false);
        foreach (var storeId in command.StoreIds)
        {
            _ = await mediator.Send(new GetStoreByIdQuery(storeId), cancellationToken).ConfigureAwait(false);
        }
        if (command.DefaultVehicleId is { } vehicleId && !await dbContext.Vehicles
            .AnyAsync(vehicle => vehicle.Id == vehicleId, cancellationToken).ConfigureAwait(false))
        {
            throw new NotFoundException($"Vehicle {vehicleId} not found.");
        }

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
