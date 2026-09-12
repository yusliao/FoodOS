using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Kpis;

namespace FSH.Modules.Inventory.Features.v1.Kpis.GetInventoryLossFacts;

public sealed class GetInventoryLossFactsQueryValidator : AbstractValidator<GetInventoryLossFactsQuery>
{
    public GetInventoryLossFactsQueryValidator()
    {
        RuleFor(x => x.Date.Year).InclusiveBetween(2000, 2100);
    }
}
