using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Shrinkage;

namespace FSH.Modules.Warehouse.Features.v1.Shrinkage.CreateShrinkage;

public sealed class CreateShrinkageCommandValidator : AbstractValidator<CreateShrinkageCommand>
{
    public CreateShrinkageCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Zone).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.LotId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(128);
    }
}
