using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Waves;

namespace FSH.Modules.Warehouse.Features.v1.Waves.AssignWave;

public sealed class AssignWaveCommandValidator : AbstractValidator<AssignWaveCommand>
{
    public AssignWaveCommandValidator()
    {
        RuleFor(x => x.WaveId).NotEmpty();
        RuleFor(x => x.PickerUserId).NotEmpty();
    }
}
