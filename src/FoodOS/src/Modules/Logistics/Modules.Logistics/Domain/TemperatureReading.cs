using FSH.Framework.Core.Domain;

namespace FSH.Modules.Logistics.Domain;

/// <summary>
/// P1 MQTT placeholder. P0 creates the table only; no ingest endpoints.
/// </summary>
public sealed class TemperatureReading : BaseEntity<Guid>
{
    public Guid VehicleId { get; private set; }
    public string Compartment { get; private set; } = default!;
    public DateTimeOffset RecordedAt { get; private set; }
    public decimal Celsius { get; private set; }
    public Guid? ShipmentId { get; private set; }

    private TemperatureReading() { }

    public static TemperatureReading Record(
        Guid vehicleId,
        string compartment,
        decimal celsius,
        DateTimeOffset recordedAt,
        Guid? shipmentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compartment);
        if (vehicleId == Guid.Empty)
        {
            throw new ArgumentException("VehicleId is required.", nameof(vehicleId));
        }

        return new TemperatureReading
        {
            Id = Guid.CreateVersion7(),
            VehicleId = vehicleId,
            Compartment = compartment.Trim(),
            RecordedAt = recordedAt,
            Celsius = celsius,
            ShipmentId = shipmentId
        };
    }
}
