using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class InboundAppointment : BaseEntity<Guid>
{
    public Guid PurchaseOrderId { get; private set; }
    public string DockSlot { get; private set; } = default!;
    public string? VehicleNo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private InboundAppointment() { }

    internal static InboundAppointment Create(Guid purchaseOrderId, string dockSlot, string? vehicleNo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dockSlot);

        return new InboundAppointment
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = purchaseOrderId,
            DockSlot = dockSlot.Trim(),
            VehicleNo = string.IsNullOrWhiteSpace(vehicleNo) ? null : vehicleNo.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
