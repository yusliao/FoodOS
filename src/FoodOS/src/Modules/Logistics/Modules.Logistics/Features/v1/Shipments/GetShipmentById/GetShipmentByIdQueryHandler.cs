using FSH.Framework.Core.Exceptions;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Shipments.GetShipmentById;

public sealed class GetShipmentByIdQueryHandler(LogisticsDbContext dbContext)
    : IQueryHandler<GetShipmentByIdQuery, ShipmentDto>
{
    public async ValueTask<ShipmentDto> Handle(GetShipmentByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var shipment = await dbContext.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.ShipmentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Shipment {query.ShipmentId} not found.");
        return shipment.ToDto();
    }
}
