using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Stock;

namespace FSH.Modules.Inventory.Features.v1.Stock.GetAvailableQty;

public sealed class GetAvailableQtyQueryValidator : AbstractValidator<GetAvailableQtyQuery>
{
    public GetAvailableQtyQueryValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
