using FSH.Framework.Shared.Persistence;
using Mediator;

namespace FSH.Modules.WmsIntegration.Contracts.v1.Mappings;

public static class WmsMappingKinds
{
    public const string Warehouse = "warehouse";
    public const string Owner = "owner";
    public const string Sku = "sku";
    public const string Supplier = "supplier";
    public const string Store = "store";
    public const string Unit = "unit";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Warehouse,
        Owner,
        Sku,
        Supplier,
        Store,
        Unit,
    };
}

public sealed record WmsMappingDto(
    Guid Id,
    string Provider,
    string ConnectionId,
    string Kind,
    string FoodOsValue,
    string ExternalValue,
    decimal FoodOsQuantityPerExternalUnit,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpsertWmsMappingCommand(
    string Kind,
    string FoodOsValue,
    string ExternalValue,
    decimal FoodOsQuantityPerExternalUnit = 1,
    bool IsActive = true) : ICommand<WmsMappingDto>;

public sealed record SearchWmsMappingsQuery(
    string? Kind = null,
    string? Search = null,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<WmsMappingDto>>;

public sealed record WmsMappingRequirement(string Kind, string FoodOsValue);

public sealed record ValidateWmsMappingsQuery(
    IReadOnlyCollection<WmsMappingRequirement> Requirements) : IQuery<WmsMappingValidationResult>;

public sealed record WmsResolvedMapping(
    string Kind,
    string FoodOsValue,
    string ExternalValue,
    decimal FoodOsQuantityPerExternalUnit);

public sealed record WmsMissingMapping(string Kind, string FoodOsValue, string Reason);

public sealed record WmsMappingValidationResult(
    bool IsValid,
    IReadOnlyCollection<WmsResolvedMapping> Resolved,
    IReadOnlyCollection<WmsMissingMapping> Missing);
