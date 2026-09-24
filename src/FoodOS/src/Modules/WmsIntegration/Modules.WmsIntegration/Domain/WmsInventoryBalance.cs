using FSH.Framework.Core.Domain;

namespace FSH.Modules.WmsIntegration.Domain;

public sealed class WmsInventoryBalance : BaseEntity<Guid>
{
    public string Provider { get; private set; } = default!;
    public string ConnectionId { get; private set; } = default!;
    public string ExternalObjectId { get; private set; } = default!;
    public string WarehouseId { get; private set; } = default!;
    public string OwnerId { get; private set; } = default!;
    public string Sku { get; private set; } = default!;
    public string Uom { get; private set; } = default!;
    public string? LotNumber { get; private set; }
    public decimal OnHandQuantity { get; private set; }
    public decimal AllocatedQuantity { get; private set; }
    public decimal AvailableQuantity { get; private set; }
    public decimal QuarantinedQuantity { get; private set; }
    public long LastSequence { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private WmsInventoryBalance() { }

    public static WmsInventoryBalance Create(
        string provider,
        string connectionId,
        string externalObjectId,
        string warehouseId,
        string ownerId,
        string sku,
        string uom,
        string? lotNumber,
        decimal onHandQuantity,
        decimal allocatedQuantity,
        decimal availableQuantity,
        decimal quarantinedQuantity,
        long sequence,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalObjectId);
        var balance = new WmsInventoryBalance
        {
            Id = Guid.CreateVersion7(),
            Provider = Normalize(provider),
            ConnectionId = Normalize(connectionId),
            ExternalObjectId = externalObjectId.Trim(),
        };
        balance.Apply(
            warehouseId, ownerId, sku, uom, lotNumber,
            onHandQuantity, allocatedQuantity, availableQuantity, quarantinedQuantity,
            sequence, occurredAt);
        return balance;
    }

    public void Apply(
        string warehouseId,
        string ownerId,
        string sku,
        string uom,
        string? lotNumber,
        decimal onHandQuantity,
        decimal allocatedQuantity,
        decimal availableQuantity,
        decimal quarantinedQuantity,
        long sequence,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(warehouseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(uom);
        if (sequence <= LastSequence)
        {
            return;
        }

        WarehouseId = Normalize(warehouseId);
        OwnerId = Normalize(ownerId);
        Sku = Normalize(sku);
        Uom = Normalize(uom);
        LotNumber = string.IsNullOrWhiteSpace(lotNumber) ? null : lotNumber.Trim().ToUpperInvariant();
        OnHandQuantity = onHandQuantity;
        AllocatedQuantity = allocatedQuantity;
        AvailableQuantity = availableQuantity;
        QuarantinedQuantity = quarantinedQuantity;
        LastSequence = sequence;
        OccurredAt = occurredAt;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
