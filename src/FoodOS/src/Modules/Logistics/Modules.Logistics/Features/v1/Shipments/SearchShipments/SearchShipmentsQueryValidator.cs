using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Shipments;

namespace FSH.Modules.Logistics.Features.v1.Shipments.SearchShipments;

public sealed class SearchShipmentsQueryValidator : AbstractValidator<SearchShipmentsQuery>
{
    public SearchShipmentsQueryValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}
