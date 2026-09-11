using FSH.Framework.Core.Exceptions;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Shipments.LoadShipment;

public sealed class LoadShipmentCommandHandler(LogisticsDbContext dbContext)
    : ICommandHandler<LoadShipmentCommand, ShipmentDto>
{
    public async ValueTask<ShipmentDto> Handle(LoadShipmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var shipment = await dbContext.Shipments
            .FirstOrDefaultAsync(s => s.Id == command.ShipmentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Shipment {command.ShipmentId} not found.");

        shipment.Load(command.OrderIds);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return shipment.ToDto();
    }
}
