using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateInboundAppointment;

public sealed class CreateInboundAppointmentCommandValidator : AbstractValidator<CreateInboundAppointmentCommand>
{
    public CreateInboundAppointmentCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.DockSlot).NotEmpty().MaximumLength(32);
        RuleFor(x => x.VehicleNo).MaximumLength(32);
    }
}
