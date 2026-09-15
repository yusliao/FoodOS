using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Logistics.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Shipments.GetCustomerDeliveries;

public sealed class GetCustomerDeliveriesQueryHandler(LogisticsDbContext dbContext)
    : IQueryHandler<GetCustomerDeliveriesQuery, IReadOnlyList<CustomerDeliveryDto>>
{
    public async ValueTask<IReadOnlyList<CustomerDeliveryDto>> Handle(
        GetCustomerDeliveriesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var storeIds = query.StoreIds.Distinct().ToList();
        if (storeIds.Count == 0)
        {
            return [];
        }

        var shipments = await dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.Stops.Any(stop => storeIds.Contains(stop.StoreId)))
            .OrderByDescending(shipment => shipment.BusinessDate)
            .ThenByDescending(shipment => shipment.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return shipments.SelectMany(shipment => shipment.Stops
            .Where(stop => storeIds.Contains(stop.StoreId))
            .OrderBy(stop => stop.Sequence)
            .Select(stop => new CustomerDeliveryDto(
                shipment.Id,
                shipment.Number,
                stop.StoreId,
                shipment.BusinessDate,
                shipment.Status.ToString(),
                stop.Status.ToString(),
                stop.Sequence,
                stop.Window,
                stop.ProofOfDelivery?.SignedAt,
                shipment.Lines
                    .Where(line => line.StoreId == stop.StoreId)
                    .Select(line => line.OrderId)
                    .Distinct()
                    .ToList())))
            .ToList();
    }
}
