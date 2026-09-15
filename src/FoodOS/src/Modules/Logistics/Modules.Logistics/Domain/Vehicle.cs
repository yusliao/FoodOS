using FSH.Framework.Core.Domain;

namespace FSH.Modules.Logistics.Domain;

public sealed class Vehicle : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public string Plate { get; private set; } = default!;
    public string CompartmentZones { get; private set; } = default!;
    public decimal PayloadKg { get; private set; }

    private Vehicle() { }

    public static Vehicle Create(string plate, string compartmentZones, decimal payloadKg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plate);
        ArgumentException.ThrowIfNullOrWhiteSpace(compartmentZones);
        if (payloadKg <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadKg), "Payload must be positive.");
        }

        return new Vehicle
        {
            Id = Guid.CreateVersion7(),
            Plate = plate.Trim().ToUpperInvariant(),
            CompartmentZones = compartmentZones.Trim(),
            PayloadKg = payloadKg
        };
    }
}
