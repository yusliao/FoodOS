using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Shipments.GetMyShipments;

public sealed class GetMyShipmentByIdQueryHandler(LogisticsDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetMyShipmentByIdQuery, ShipmentDto>
{
    public async ValueTask<ShipmentDto> Handle(
        GetMyShipmentByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Guid userId = currentUser.GetUserId();
        var shipment = await dbContext.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.Id == query.ShipmentId
                && dbContext.Drivers.Any(driver => driver.Id == item.DriverId && driver.UserId == userId),
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Shipment {query.ShipmentId} not found.");
        return shipment.ToDto();
    }
}
