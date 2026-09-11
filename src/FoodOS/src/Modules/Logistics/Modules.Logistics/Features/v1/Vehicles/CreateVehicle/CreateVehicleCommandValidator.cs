using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Vehicles;

namespace FSH.Modules.Logistics.Features.v1.Vehicles.CreateVehicle;

public sealed class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x.Plate).NotEmpty().MaximumLength(16);
        RuleFor(x => x.CompartmentZones).NotEmpty().MaximumLength(64);
        RuleFor(x => x.PayloadKg).GreaterThan(0);
    }
}
