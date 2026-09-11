using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Waves;

namespace FSH.Modules.Warehouse.Features.v1.Waves.GenerateWave;

public sealed class GenerateWaveCommandValidator : AbstractValidator<GenerateWaveCommand>
{
    public GenerateWaveCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}
