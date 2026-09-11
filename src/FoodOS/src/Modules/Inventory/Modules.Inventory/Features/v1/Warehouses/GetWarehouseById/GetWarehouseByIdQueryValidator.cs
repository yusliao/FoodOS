using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.GetWarehouseById;

public sealed class GetWarehouseByIdQueryValidator : AbstractValidator<GetWarehouseByIdQuery>
{
    public GetWarehouseByIdQueryValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}
