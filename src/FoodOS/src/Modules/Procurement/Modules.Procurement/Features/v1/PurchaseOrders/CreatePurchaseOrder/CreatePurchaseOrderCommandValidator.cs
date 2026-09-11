using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreatePurchaseOrder;

public sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Zone).NotEmpty().MaximumLength(16);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}
