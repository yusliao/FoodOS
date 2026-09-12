using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Kpis;

namespace FSH.Modules.Ordering.Features.v1.Kpis.GetOrderKpiFacts;

public sealed class GetOrderKpiFactsQueryValidator : AbstractValidator<GetOrderKpiFactsQuery>
{
    public GetOrderKpiFactsQueryValidator()
    {
        RuleFor(x => x.Date.Year).InclusiveBetween(2000, 2100);
    }
}
