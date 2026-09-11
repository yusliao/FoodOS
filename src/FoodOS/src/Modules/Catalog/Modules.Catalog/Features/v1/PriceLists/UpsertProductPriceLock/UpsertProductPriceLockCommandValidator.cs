using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.UpsertProductPriceLock;

public sealed class UpsertProductPriceLockCommandValidator : AbstractValidator<UpsertProductPriceLockCommand>
{
    public UpsertProductPriceLockCommandValidator()
    {
        RuleFor(x => x.CustomerOrgId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Until).Must(v => v != default).WithMessage("Until is required.");
    }
}
