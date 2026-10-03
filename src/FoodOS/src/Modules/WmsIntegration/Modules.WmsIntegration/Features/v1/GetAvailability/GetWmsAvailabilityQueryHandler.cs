using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.WmsIntegration.Contracts.v1;
using Mediator;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Features.v1.GetAvailability;

public sealed class GetWmsAvailabilityQueryHandler(
    IWmsAvailabilityReader reader,
    IOptions<WmsIntegrationOptions> options,
    ICurrentUser currentUser) : IQueryHandler<GetWmsAvailabilityQuery, WmsProductAvailabilityDto>
{
    public async ValueTask<WmsProductAvailabilityDto> Handle(GetWmsAvailabilityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!currentUser.IsAuthenticated()
            || !string.Equals(currentUser.GetTenant(), MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Inventory quantities require an operator identity.");
        }

        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            return new(settings.WarehouseId, new(query.Sku, query.Uom, 0, false, null, "notConfigured"));
        }

        var result = await reader.GetAvailabilityAsync(settings.WarehouseId,
            [new(query.Sku, query.Uom, 1m)], cancellationToken).ConfigureAwait(false);
        return new(settings.WarehouseId, result.Single());
    }
}
