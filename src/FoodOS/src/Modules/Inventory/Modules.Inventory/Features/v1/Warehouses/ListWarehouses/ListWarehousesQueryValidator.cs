using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;

namespace FSH.Modules.Inventory.Features.v1.Warehouses.ListWarehouses;

public sealed class ListWarehousesQueryValidator : AbstractValidator<ListWarehousesQuery>
{
    public ListWarehousesQueryValidator()
    {
        RuleFor(x => x).NotNull();
    }
}
