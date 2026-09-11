using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Shipments;

namespace FSH.Modules.Logistics.Features.v1.Shipments.DepartShipment;

public sealed class DepartShipmentCommandValidator : AbstractValidator<DepartShipmentCommand>
{
    public DepartShipmentCommandValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty();
    }
}
