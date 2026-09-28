using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using Mediator;

namespace FSH.Modules.Ordering.Services;

internal static class StoreWarehouseResolver
{
    public static async Task<WarehouseDto?> ResolveAsync(
        IMediator mediator, Guid defaultWarehouseId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        if (defaultWarehouseId != Guid.Empty)
        {
            return await mediator.Send(new GetWarehouseByIdQuery(defaultWarehouseId), cancellationToken)
                .ConfigureAwait(false);
        }

        var warehouses = await mediator.Send(new ListWarehousesQuery(), cancellationToken).ConfigureAwait(false);
        if (warehouses.Count > 1)
        {
            throw new CustomException(
                "An unassigned store requires exactly one operator warehouse before ordering.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }
        return warehouses.Count == 1 ? warehouses[0] : null;
    }
}
