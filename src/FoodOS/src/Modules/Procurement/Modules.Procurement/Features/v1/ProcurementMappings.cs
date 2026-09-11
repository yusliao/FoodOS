using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Domain;

namespace FSH.Modules.Procurement.Features.v1;

internal static class ProcurementMappings
{
    public static SupplierDto ToDto(this Supplier supplier)
        => new(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.Categories,
            supplier.LeadDays,
            supplier.Status,
            supplier.CreatedAtUtc);

    public static PurchaseOrderDto ToDto(this PurchaseOrder po)
        => new(
            po.Id,
            po.Number,
            po.SupplierId,
            po.WarehouseId,
            po.Status.ToString(),
            po.ExpectedAt,
            po.CreatedAt,
            po.Lines.Select(l => new PurchaseOrderLineDto(
                l.Id,
                l.ProductId,
                l.Zone,
                l.Quantity,
                l.ReceivedQty,
                l.RejectedQty)).ToList(),
            po.Appointment is null
                ? null
                : new InboundAppointmentDto(
                    po.Appointment.Id,
                    po.Appointment.PurchaseOrderId,
                    po.Appointment.DockSlot,
                    po.Appointment.VehicleNo,
                    po.Appointment.CreatedAt),
            po.QualityChecks.Select(c => new QualityCheckDto(
                c.Id,
                c.PurchaseOrderId,
                c.LineId,
                c.InspectorUserId,
                c.Result.ToString(),
                c.SampleQty,
                c.Quantity,
                c.LotNo,
                c.LotId,
                c.Note,
                c.PhotoIds(),
                c.CheckedAt)).ToList());
}
