using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Locations;

namespace FSH.Modules.Warehouse.Features.v1.Locations.SearchLocations;

public sealed class SearchLocationsQueryValidator : AbstractValidator<SearchLocationsQuery>
{
    public SearchLocationsQueryValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}
