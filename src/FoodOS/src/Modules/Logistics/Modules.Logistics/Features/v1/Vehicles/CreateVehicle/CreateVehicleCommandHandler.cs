using FSH.Modules.Logistics.Contracts.v1.Vehicles;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Vehicles.CreateVehicle;

public sealed class CreateVehicleCommandHandler(LogisticsDbContext dbContext)
    : ICommandHandler<CreateVehicleCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateVehicleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        string plate = command.Plate.Trim().ToUpperInvariant();
        var existing = await dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.Plate == plate, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Id;
        }

        var vehicle = Vehicle.Create(plate, command.CompartmentZones, command.PayloadKg);
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return vehicle.Id;
    }
}
