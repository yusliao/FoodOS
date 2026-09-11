using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Locations;

namespace FSH.Modules.Warehouse.Features.v1.Locations.CreateLocation;

public sealed class CreateLocationCommandValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ZoneId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(16);
    }
}
