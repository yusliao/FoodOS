using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

public sealed record CreateInboundAppointmentCommand(
    Guid PurchaseOrderId,
    string DockSlot,
    string? VehicleNo = null) : ICommand<Guid>;
