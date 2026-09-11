using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.v1.Suppliers;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Suppliers.CreateSupplier;

public sealed class CreateSupplierCommandHandler(ProcurementDbContext dbContext)
    : ICommandHandler<CreateSupplierCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplier = Supplier.Create(command.Code, command.Name, command.Categories, command.LeadDays);
        bool taken = await dbContext.Suppliers
            .AnyAsync(s => s.Code == supplier.Code, cancellationToken)
            .ConfigureAwait(false);
        if (taken)
        {
            throw new CustomException(
                $"A supplier with code '{supplier.Code}' already exists.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return supplier.Id;
    }
}
