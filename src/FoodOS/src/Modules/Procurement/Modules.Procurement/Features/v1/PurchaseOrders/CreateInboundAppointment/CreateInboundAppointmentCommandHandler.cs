using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateInboundAppointment;

public sealed class CreateInboundAppointmentCommandHandler(ProcurementDbContext dbContext)
    : ICommandHandler<CreateInboundAppointmentCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateInboundAppointmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var po = await dbContext.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == command.PurchaseOrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order {command.PurchaseOrderId} not found.");

        var appointment = po.Appoint(command.DockSlot, command.VehicleNo);
        dbContext.InboundAppointments.Add(appointment);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return appointment.Id;
    }
}
