using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Waves;

namespace FSH.Modules.Warehouse.Features.v1.Waves.SearchWaves;

public sealed class SearchWavesQueryValidator : AbstractValidator<SearchWavesQuery>
{
    public SearchWavesQueryValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}
