using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.CreateWarehouse;

public sealed class CreateWarehouseCommandHandler(InventoryDbContext dbContext)
    : ICommandHandler<CreateWarehouseCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var warehouse = Warehouse.Create(
            command.Code,
            command.Name,
            command.City,
            string.IsNullOrWhiteSpace(command.TimeZoneId)
                ? null
                : OperatingClock.Default(command.TimeZoneId));

        bool taken = await dbContext.Warehouses
            .AnyAsync(w => w.Code == warehouse.Code, cancellationToken)
            .ConfigureAwait(false);
        if (taken)
        {
            throw new CustomException(
                $"A warehouse with code '{warehouse.Code}' already exists.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return warehouse.Id;
    }
}
