using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Warehouse.Contracts.v1.Locations;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Locations.CreateLocation;

public sealed class CreateLocationCommandHandler(WarehouseDbContext dbContext)
    : ICommandHandler<CreateLocationCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateLocationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.TryParse<LocationType>(command.Type, ignoreCase: true, out var type))
        {
            throw new CustomException(
                $"Unknown location type '{command.Type}'.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        string code = command.Code.Trim().ToUpperInvariant();
        var existing = await dbContext.Locations
            .FirstOrDefaultAsync(
                l => l.WarehouseId == command.WarehouseId && l.Code == code,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Id;
        }

        var location = Location.Create(command.WarehouseId, command.ZoneId, code, type);
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return location.Id;
    }
}
