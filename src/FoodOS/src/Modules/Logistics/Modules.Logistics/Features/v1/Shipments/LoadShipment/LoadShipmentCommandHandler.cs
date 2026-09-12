using FSH.Framework.Core.Exceptions;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Features.v1;
using FSH.Modules.Warehouse.Contracts.v1.Pack;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Shipments.LoadShipment;

public sealed class LoadShipmentCommandHandler(LogisticsDbContext dbContext, IMediator mediator)
    : ICommandHandler<LoadShipmentCommand, ShipmentDto>
{
    public async ValueTask<ShipmentDto> Handle(LoadShipmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var shipment = await dbContext.Shipments
            .FirstOrDefaultAsync(s => s.Id == command.ShipmentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Shipment {command.ShipmentId} not found.");

        var orderIds = (command.OrderIds ?? []).ToList();
        if (command.ToteIds is { Count: > 0 })
        {
            var fromTotes = await mediator
                .Send(new ListOrdersForTotesQuery(command.ToteIds), cancellationToken)
                .ConfigureAwait(false);
            orderIds.AddRange(fromTotes);
        }

        shipment.Load(orderIds.Distinct().ToList());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return shipment.ToDto();
    }
}
