using System.Net;
using FSH.Framework.Core.Domain;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Logistics.Domain;

public sealed class Shipment : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    private readonly List<ShipmentStop> _stops = [];
    private readonly List<ShipmentLine> _lines = [];
    private readonly List<ReturnOnTruck> _returns = [];

    public string Number { get; private set; } = default!;
    public Guid RouteId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid DriverId { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<ShipmentStop> Stops => _stops;
    public IReadOnlyList<ShipmentLine> Lines => _lines;
    public IReadOnlyList<ReturnOnTruck> Returns => _returns;

    private Shipment() { }

    public static Shipment Create(
        string number,
        Guid routeId,
        Guid warehouseId,
        DateOnly businessDate,
        Guid vehicleId,
        Guid driverId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        if (routeId == Guid.Empty || warehouseId == Guid.Empty || vehicleId == Guid.Empty || driverId == Guid.Empty)
        {
            throw new ArgumentException("Route, warehouse, vehicle and driver are required.");
        }

        return new Shipment
        {
            Id = Guid.CreateVersion7(),
            Number = number.Trim().ToUpperInvariant(),
            RouteId = routeId,
            WarehouseId = warehouseId,
            BusinessDate = businessDate,
            VehicleId = vehicleId,
            DriverId = driverId,
            Status = ShipmentStatus.Created,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public ShipmentStop AddStop(Guid storeId, int sequence, string? window)
    {
        var stop = ShipmentStop.Create(Id, storeId, sequence, window);
        _stops.Add(stop);
        return stop;
    }

    public ShipmentLine AddLine(Guid orderId, Guid storeId, Guid? toteId = null)
    {
        var line = ShipmentLine.Create(Id, orderId, storeId, toteId);
        _lines.Add(line);
        return line;
    }

    public void Load(IReadOnlyList<Guid> scannedOrderIds)
    {
        ArgumentNullException.ThrowIfNull(scannedOrderIds);
        if (Status is ShipmentStatus.Loading or ShipmentStatus.Departed or ShipmentStatus.Completed)
        {
            return;
        }

        if (Status != ShipmentStatus.Created)
        {
            throw new CustomException(
                "Only created shipments can be loaded.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var expected = _lines.Select(l => l.OrderId).OrderBy(id => id).ToList();
        var scanned = scannedOrderIds.Distinct().OrderBy(id => id).ToList();
        if (expected.Count == 0 || expected.Count != scanned.Count || !expected.SequenceEqual(scanned))
        {
            throw new CustomException(
                "Scanned orders must match every packed order on this shipment.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        Status = ShipmentStatus.Loading;
    }

    public void Depart()
    {
        if (Status is ShipmentStatus.Departed or ShipmentStatus.Completed)
        {
            return;
        }

        if (Status != ShipmentStatus.Loading)
        {
            throw new CustomException(
                "Shipment must be loaded before departure.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        Status = ShipmentStatus.Departed;
    }

    public ReturnOnTruck RecordReturn(Guid orderId, Guid productId, Guid lotId, decimal quantity, string reason, Guid? returnId = null)
    {
        var ret = ReturnOnTruck.Create(Id, orderId, productId, lotId, quantity, reason, returnId);
        _returns.Add(ret);
        return ret;
    }

    public ProofOfDelivery ConfirmStop(
        Guid stopId,
        string signedQtyJson,
        IReadOnlyList<Guid> photoFileIds,
        string signerName,
        string? geo)
    {
        PrepareStopSignature(stopId, signedQtyJson, photoFileIds, signerName, geo);
        var pod = _stops.First(s => s.Id == stopId).ConfirmPod(signedQtyJson, photoFileIds, signerName, geo);
        if (_stops.TrueForAll(s => s.Status == StopStatus.Delivered))
        {
            Status = ShipmentStatus.Completed;
        }

        return pod;
    }

    public ProofOfDelivery PrepareStopSignature(
        Guid stopId,
        string signedQtyJson,
        IReadOnlyList<Guid> photoFileIds,
        string signerName,
        string? geo)
    {
        if (Status != ShipmentStatus.Departed && Status != ShipmentStatus.Completed)
        {
            throw new CustomException(
                "Proof of delivery is only allowed after departure.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var stop = _stops.Find(s => s.Id == stopId)
            ?? throw new CustomException(
                $"Stop {stopId} was not found on this shipment.",
                (IEnumerable<string>?)null,
                HttpStatusCode.NotFound);

        return stop.PreparePod(signedQtyJson, photoFileIds, signerName, geo);
    }
}
