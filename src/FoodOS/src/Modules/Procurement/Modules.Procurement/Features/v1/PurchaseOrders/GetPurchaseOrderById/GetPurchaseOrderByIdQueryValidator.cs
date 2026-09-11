using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.GetPurchaseOrderById;

public sealed class GetPurchaseOrderByIdQueryValidator : AbstractValidator<GetPurchaseOrderByIdQuery>
{
    public GetPurchaseOrderByIdQueryValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
    }
}
