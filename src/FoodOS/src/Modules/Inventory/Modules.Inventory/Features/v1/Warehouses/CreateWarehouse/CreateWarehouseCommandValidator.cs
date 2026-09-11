using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.CreateWarehouse;

public sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.City).NotEmpty().MaximumLength(128);
        RuleFor(x => x.TimeZoneId).MaximumLength(64);
    }
}
