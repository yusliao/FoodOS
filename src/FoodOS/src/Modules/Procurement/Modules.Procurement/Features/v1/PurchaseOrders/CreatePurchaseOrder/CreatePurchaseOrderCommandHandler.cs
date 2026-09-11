using System.Globalization;
using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Procurement.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreatePurchaseOrder;

public sealed class CreatePurchaseOrderCommandHandler(ProcurementDbContext dbContext, IMediator mediator)
    : ICommandHandler<CreatePurchaseOrderCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool supplierExists = await dbContext.Suppliers
            .AnyAsync(s => s.Id == command.SupplierId, cancellationToken)
            .ConfigureAwait(false);
        if (!supplierExists)
        {
            throw new NotFoundException($"Supplier {command.SupplierId} not found.");
        }

        _ = await mediator.Send(new GetWarehouseByIdQuery(command.WarehouseId), cancellationToken)
            .ConfigureAwait(false);

        var canonicalLines = command.Lines
            .Select(l => (l.ProductId, Zone: ZoneKinds.Parse(l.Zone).ToString(), l.Quantity))
            .ToList();

        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        string prefix = "PO" + utcNow.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        int todayCount = await dbContext.PurchaseOrders
            .CountAsync(p => p.Number.StartsWith(prefix), cancellationToken)
            .ConfigureAwait(false);

        var po = PurchaseOrder.Create(
            PurchaseOrderNumbers.Next(todayCount, utcNow),
            command.SupplierId,
            command.WarehouseId,
            command.ExpectedAt,
            canonicalLines);

        dbContext.PurchaseOrders.Add(po);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            throw new CustomException(
                $"A purchase order with number '{po.Number}' already exists.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        return po.Id;
    }
}
