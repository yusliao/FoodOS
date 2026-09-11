using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Shipments;

namespace FSH.Modules.Logistics.Features.v1.Shipments.LoadShipment;

public sealed class LoadShipmentCommandValidator : AbstractValidator<LoadShipmentCommand>
{
    public LoadShipmentCommandValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty();
        RuleFor(x => x.OrderIds).NotEmpty();
        RuleForEach(x => x.OrderIds).NotEmpty();
    }
}
