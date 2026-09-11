using System.Globalization;
using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Plans;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Waves.GenerateWave;

public sealed class GenerateWaveCommandHandler(WarehouseDbContext dbContext, IMediator mediator, TimeProvider clock)
    : ICommandHandler<GenerateWaveCommand, IReadOnlyList<WaveDto>>
{
    public async ValueTask<IReadOnlyList<WaveDto>> Handle(
        GenerateWaveCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var warehouse = await mediator
            .Send(new GetWarehouseByIdQuery(command.WarehouseId), cancellationToken)
            .ConfigureAwait(false);

        DateOnly businessDate = command.BusinessDate ?? ResolveBusinessDate(warehouse, clock.GetUtcNow());
        var plan = await mediator
            .Send(new GetDailyPlanQuery(warehouse.Id, businessDate), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new CustomException(
                "Daily plan is not open. Trigger cutoff first.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);

        var orders = await mediator
            .Send(new ListOrdersForWaveQuery(warehouse.Id, businessDate), cancellationToken)
            .ConfigureAwait(false);

        var zoneGroups = orders
            .SelectMany(o => o.Lines.Select(l => (Order: o, Line: l)))
            .GroupBy(x => x.Line.Zone, StringComparer.OrdinalIgnoreCase);

        var result = new List<Wave>();
        foreach (var group in zoneGroups)
        {
            var zoneDto = warehouse.Zones.FirstOrDefault(z =>
                string.Equals(z.Kind, group.Key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(z.Code, group.Key, StringComparison.OrdinalIgnoreCase))
                ?? throw new CustomException(
                    $"Warehouse has no zone '{group.Key}'.",
                    (IEnumerable<string>?)null,
                    HttpStatusCode.BadRequest);

            var existing = await dbContext.Waves
                .FirstOrDefaultAsync(
                    w => w.DailyPlanId == plan.Id && w.ZoneId == zoneDto.Id,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                result.Add(existing);
                continue;
            }

            var location = await EnsurePickLocationAsync(
                    warehouse.Id,
                    zoneDto.Id,
                    zoneDto.Code,
                    cancellationToken)
                .ConfigureAwait(false);

            string number = await WaveNumbers
                .NextAsync(dbContext, warehouse.Code, zoneDto.Kind, businessDate, cancellationToken)
                .ConfigureAwait(false);
            var wave = Wave.Create(number, plan.Id, warehouse.Id, zoneDto.Id, zoneDto.Kind, businessDate);
            foreach (var (order, line) in group)
            {
                wave.AddTask(
                    order.Id,
                    line.Id,
                    line.ReservationId,
                    line.ProductId,
                    line.Zone,
                    location.Id,
                    line.OrderedQty);
            }

            dbContext.Waves.Add(wave);
            foreach (var task in wave.Tasks)
            {
                dbContext.PickTasks.Add(task);
            }

            result.Add(wave);
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return result.Select(w => w.ToDto()).ToList();
    }

    private static DateOnly ResolveBusinessDate(FSH.Modules.Inventory.Contracts.Dtos.WarehouseDto warehouse, DateTimeOffset utcNow)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(warehouse.Clock.TimeZoneId);
        TimeOnly cutoffLocal = TimeOnly.ParseExact(
            warehouse.Clock.CutoffLocal,
            "HH:mm",
            CultureInfo.InvariantCulture);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, tz);
        DateOnly today = DateOnly.FromDateTime(localNow.DateTime);
        var localCutoff = today.ToDateTime(cutoffLocal, DateTimeKind.Unspecified);
        TimeSpan offset = tz.GetUtcOffset(localCutoff);
        var todayCutoff = new DateTimeOffset(localCutoff, offset);
        return localNow < todayCutoff ? today : today.AddDays(1);
    }

    private async Task<Location> EnsurePickLocationAsync(
        Guid warehouseId,
        Guid zoneId,
        string zoneCode,
        CancellationToken cancellationToken)
    {
        string code = $"{zoneCode.Trim().ToUpperInvariant()}-PICK";
        var location = await dbContext.Locations
            .FirstOrDefaultAsync(l => l.WarehouseId == warehouseId && l.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (location is not null)
        {
            return location;
        }

        location = Location.Create(warehouseId, zoneId, code, LocationType.Pick);
        dbContext.Locations.Add(location);
        return location;
    }
}
