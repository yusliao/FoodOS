using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Waves;

namespace FSH.Modules.Warehouse.Features.v1.Waves.GetWaveById;

public sealed class GetWaveByIdQueryValidator : AbstractValidator<GetWaveByIdQuery>
{
    public GetWaveByIdQueryValidator()
    {
        RuleFor(x => x.WaveId).NotEmpty();
    }
}
