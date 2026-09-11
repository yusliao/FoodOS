using FSH.Framework.Core.Context;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using FSH.Modules.Logistics.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Shipments.GetMyShipments;

public sealed class GetMyShipmentsQueryHandler(LogisticsDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetMyShipmentsQuery, IReadOnlyList<ShipmentDto>>
{
    public async ValueTask<IReadOnlyList<ShipmentDto>> Handle(
        GetMyShipmentsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Guid userId = currentUser.GetUserId();
        var shipments = await dbContext.Shipments
            .AsNoTracking()
            .Where(s =>
                dbContext.Drivers.Any(d => d.Id == s.DriverId && d.UserId == userId)
                && (s.Status == ShipmentStatus.Loading
                    || s.Status == ShipmentStatus.Departed
                    || s.Status == ShipmentStatus.Completed))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return shipments.Select(s => s.ToDto()).ToList();
    }
}
