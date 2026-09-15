using System.Net;
using FSH.Framework.Core.Domain;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Warehouse.Domain;

public sealed class PackTote : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    private readonly List<Guid> _orderIds = [];

    public Guid WaveId { get; private set; }
    public string Sscc { get; private set; } = default!;
    public Guid? DockLocationId { get; private set; }
    public PackToteStatus Status { get; private set; }
    public DateTimeOffset PackedAt { get; private set; }

    public IReadOnlyList<Guid> OrderIds => _orderIds;

    private PackTote() { }

    public static PackTote Create(Guid waveId, string sscc, IReadOnlyList<Guid> orderIds, Guid? dockLocationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sscc);
        ArgumentNullException.ThrowIfNull(orderIds);
        if (waveId == Guid.Empty)
        {
            throw new ArgumentException("WaveId is required.", nameof(waveId));
        }

        if (orderIds.Count == 0 || orderIds.Any(id => id == Guid.Empty))
        {
            throw new CustomException(
                "Pack tote requires at least one order.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        var tote = new PackTote
        {
            Id = Guid.CreateVersion7(),
            WaveId = waveId,
            Sscc = sscc.Trim().ToUpperInvariant(),
            DockLocationId = dockLocationId,
            Status = PackToteStatus.Packed,
            PackedAt = DateTimeOffset.UtcNow
        };
        tote._orderIds.AddRange(orderIds.Distinct());
        return tote;
    }
}
