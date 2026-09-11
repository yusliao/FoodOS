using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Suppliers;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Suppliers.GetSupplierById;

public sealed class GetSupplierByIdQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<GetSupplierByIdQuery, SupplierDto>
{
    public async ValueTask<SupplierDto> Handle(GetSupplierByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var supplier = await dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.SupplierId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Supplier {query.SupplierId} not found.");

        return supplier.ToDto();
    }
}
