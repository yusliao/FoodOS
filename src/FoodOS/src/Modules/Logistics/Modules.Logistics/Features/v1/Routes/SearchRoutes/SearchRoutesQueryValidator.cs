using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Routes;

namespace FSH.Modules.Logistics.Features.v1.Routes.SearchRoutes;

public sealed class SearchRoutesQueryValidator : AbstractValidator<SearchRoutesQuery>
{
    public SearchRoutesQueryValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}
