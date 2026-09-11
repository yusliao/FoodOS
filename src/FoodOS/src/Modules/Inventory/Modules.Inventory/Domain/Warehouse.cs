using FSH.Framework.Core.Domain;
using FSH.Modules.Inventory.Contracts;

namespace FSH.Modules.Inventory.Domain;

public sealed class Warehouse : AggregateRoot<Guid>
{
    private readonly List<TemperatureZone> _zones = [];

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string City { get; private set; } = default!;
    public string TimeZoneId { get; private set; } = default!;
    public OperatingClock Clock { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<TemperatureZone> Zones => _zones;

    private Warehouse() { }

    public static Warehouse Create(string code, string name, string city, OperatingClock? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        var warehouse = new Warehouse
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            City = city.Trim(),
            Clock = clock ?? OperatingClock.Default(),
            CreatedAtUtc = DateTime.UtcNow
        };
        warehouse.TimeZoneId = warehouse.Clock.TimeZoneId;
        warehouse._zones.Add(TemperatureZone.Create(warehouse.Id, TemperatureZoneKind.Ambient));
        warehouse._zones.Add(TemperatureZone.Create(warehouse.Id, TemperatureZoneKind.Chilled));
        warehouse._zones.Add(TemperatureZone.Create(warehouse.Id, TemperatureZoneKind.Frozen));
        return warehouse;
    }

    public TemperatureZone ZoneOf(TemperatureZoneKind kind)
        => _zones.First(z => z.Kind == kind);

    public void ReplaceClock(OperatingClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Clock = clock;
        TimeZoneId = clock.TimeZoneId;
    }
}
