using System.Net;
using FSH.Framework.Core.Domain;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Warehouse.Domain;

public sealed class Wave : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    private readonly List<PickTask> _tasks = [];

    public string Number { get; private set; } = default!;
    public Guid DailyPlanId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ZoneId { get; private set; }
    public string Zone { get; private set; } = default!;
    public Guid? RouteId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public WaveStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<PickTask> Tasks => _tasks;

    private Wave() { }

    public static Wave Create(
        string number,
        Guid dailyPlanId,
        Guid warehouseId,
        Guid zoneId,
        string zone,
        DateOnly businessDate,
        Guid? routeId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(zone);
        if (dailyPlanId == Guid.Empty)
        {
            throw new ArgumentException("DailyPlanId is required.", nameof(dailyPlanId));
        }

        if (warehouseId == Guid.Empty)
        {
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));
        }

        if (zoneId == Guid.Empty)
        {
            throw new ArgumentException("ZoneId is required.", nameof(zoneId));
        }

        return new Wave
        {
            Id = Guid.CreateVersion7(),
            Number = number.Trim().ToUpperInvariant(),
            DailyPlanId = dailyPlanId,
            WarehouseId = warehouseId,
            ZoneId = zoneId,
            Zone = zone.Trim(),
            RouteId = routeId,
            BusinessDate = businessDate,
            Status = WaveStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public PickTask AddTask(
        Guid orderId,
        Guid orderLineId,
        Guid? reservationId,
        Guid productId,
        string zone,
        Guid locationId,
        decimal quantity)
    {
        var task = PickTask.Create(Id, orderId, orderLineId, reservationId, productId, zone, locationId, quantity);
        _tasks.Add(task);
        return task;
    }

    public void Release()
    {
        if (Status is WaveStatus.Released or WaveStatus.Picking or WaveStatus.Completed)
        {
            return;
        }

        if (Status != WaveStatus.Draft)
        {
            throw new CustomException(
                "Only draft waves can be released.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        Status = WaveStatus.Released;
    }

    public void MarkPicking()
    {
        if (Status == WaveStatus.Picking || Status == WaveStatus.Completed)
        {
            return;
        }

        if (Status != WaveStatus.Released)
        {
            throw new CustomException(
                "Wave must be released before picking.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        Status = WaveStatus.Picking;
    }

    public void CompleteIfDone()
    {
        if (_tasks.Count == 0 || !_tasks.TrueForAll(t => t.IsComplete))
        {
            return;
        }

        Status = WaveStatus.Completed;
    }
}
