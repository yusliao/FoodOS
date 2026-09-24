using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
using FSH.Modules.WmsIntegration.Domain;

namespace FSH.Modules.WmsIntegration.Features.v1.Mappings;

internal static class WmsMappingDtoMapper
{
    internal static WmsMappingDto ToDto(this WmsMapping mapping) => new(
        mapping.Id,
        mapping.Provider,
        mapping.ConnectionId,
        mapping.Kind,
        mapping.FoodOsValue,
        mapping.ExternalValue,
        mapping.FoodOsQuantityPerExternalUnit,
        mapping.IsActive,
        mapping.CreatedAtUtc,
        mapping.UpdatedAtUtc);
}
