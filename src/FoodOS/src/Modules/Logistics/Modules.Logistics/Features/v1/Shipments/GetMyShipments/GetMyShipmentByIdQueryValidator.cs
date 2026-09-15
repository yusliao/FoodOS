using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Shipments;

namespace FSH.Modules.Logistics.Features.v1.Shipments.GetMyShipments;

public sealed class GetMyShipmentByIdQueryValidator : AbstractValidator<GetMyShipmentByIdQuery>
{
    public GetMyShipmentByIdQueryValidator()
    {
        RuleFor(query => query.ShipmentId).NotEmpty();
    }
}
