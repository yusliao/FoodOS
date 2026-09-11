using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Suppliers;

public sealed record GetSupplierByIdQuery(Guid SupplierId) : IQuery<SupplierDto>;
