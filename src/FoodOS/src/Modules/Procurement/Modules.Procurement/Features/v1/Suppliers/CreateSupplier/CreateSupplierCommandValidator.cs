using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.Suppliers;

namespace FSH.Modules.Procurement.Features.v1.Suppliers.CreateSupplier;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Categories).MaximumLength(256);
        RuleFor(x => x.LeadDays).GreaterThanOrEqualTo(0);
    }
}
