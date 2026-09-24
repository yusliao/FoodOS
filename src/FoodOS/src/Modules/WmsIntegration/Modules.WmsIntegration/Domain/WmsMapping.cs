using FSH.Framework.Core.Domain;

namespace FSH.Modules.WmsIntegration.Domain;

public sealed class WmsMapping : BaseEntity<Guid>
{
    public string Provider { get; private set; } = default!;
    public string ConnectionId { get; private set; } = default!;
    public string Kind { get; private set; } = default!;
    public string FoodOsValue { get; private set; } = default!;
    public string ExternalValue { get; private set; } = default!;
    public decimal FoodOsQuantityPerExternalUnit { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private WmsMapping() { }

    public static WmsMapping Create(
        string provider,
        string connectionId,
        string kind,
        string foodOsValue,
        string externalValue,
        decimal foodOsQuantityPerExternalUnit,
        bool isActive)
    {
        DateTime utcNow = DateTime.UtcNow;
        return new WmsMapping
        {
            Id = Guid.CreateVersion7(),
            Provider = provider,
            ConnectionId = connectionId,
            Kind = kind,
            FoodOsValue = foodOsValue,
            ExternalValue = externalValue,
            FoodOsQuantityPerExternalUnit = foodOsQuantityPerExternalUnit,
            IsActive = isActive,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void Update(string externalValue, decimal foodOsQuantityPerExternalUnit, bool isActive)
    {
        ExternalValue = externalValue;
        FoodOsQuantityPerExternalUnit = foodOsQuantityPerExternalUnit;
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
