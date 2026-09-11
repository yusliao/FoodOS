using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Waves;

namespace FSH.Modules.Warehouse.Features.v1.Waves.StartWave;

public sealed class StartWaveCommandValidator : AbstractValidator<StartWaveCommand>
{
    public StartWaveCommandValidator()
    {
        RuleFor(x => x.WaveId).NotEmpty();
    }
}
