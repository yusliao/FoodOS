using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.Suppliers;

namespace FSH.Modules.Procurement.Features.v1.Suppliers.GetSupplierById;

public sealed class GetSupplierByIdQueryValidator : AbstractValidator<GetSupplierByIdQuery>
{
    public GetSupplierByIdQueryValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
    }
}
