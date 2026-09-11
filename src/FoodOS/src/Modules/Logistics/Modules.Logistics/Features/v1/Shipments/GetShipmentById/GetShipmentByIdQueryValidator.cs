using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Shipments;

namespace FSH.Modules.Logistics.Features.v1.Shipments.GetShipmentById;

public sealed class GetShipmentByIdQueryValidator : AbstractValidator<GetShipmentByIdQuery>
{
    public GetShipmentByIdQueryValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty();
    }
}
