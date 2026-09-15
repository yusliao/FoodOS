using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Shipments;

namespace FSH.Modules.Logistics.Features.v1.Shipments.GetCustomerDeliveries;

public sealed class GetCustomerDeliveriesQueryValidator : AbstractValidator<GetCustomerDeliveriesQuery>
{
    public GetCustomerDeliveriesQueryValidator()
    {
        RuleFor(query => query.StoreIds).NotEmpty().Must(storeIds => storeIds.Count <= 100);
        RuleForEach(query => query.StoreIds).NotEmpty();
    }
}
