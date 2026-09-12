using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Pack;

namespace FSH.Modules.Warehouse.Features.v1.Pack.CreatePackTote;

public sealed class CreatePackToteCommandValidator : AbstractValidator<CreatePackToteCommand>
{
    public CreatePackToteCommandValidator()
    {
        RuleFor(x => x.WaveId).NotEmpty();
        RuleFor(x => x.OrderIds).NotEmpty();
        RuleForEach(x => x.OrderIds).NotEmpty();
        RuleFor(x => x.Sscc).MaximumLength(48);
    }
}
