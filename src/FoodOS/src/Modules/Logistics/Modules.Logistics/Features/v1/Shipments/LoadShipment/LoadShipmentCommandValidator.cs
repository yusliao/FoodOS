using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Shipments;

namespace FSH.Modules.Logistics.Features.v1.Shipments.LoadShipment;

public sealed class LoadShipmentCommandValidator : AbstractValidator<LoadShipmentCommand>
{
    public LoadShipmentCommandValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty();
        RuleFor(x => x)
            .Must(x => (x.OrderIds?.Count ?? 0) > 0 || (x.ToteIds?.Count ?? 0) > 0)
            .WithMessage("Scan at least one order or tote.");
    }
}
