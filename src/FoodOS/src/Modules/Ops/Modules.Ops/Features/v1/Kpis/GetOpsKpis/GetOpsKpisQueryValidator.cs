using FluentValidation;
using FSH.Modules.Ops.Contracts.v1.Kpis;

namespace FSH.Modules.Ops.Features.v1.Kpis.GetOpsKpis;

public sealed class GetOpsKpisQueryValidator : AbstractValidator<GetOpsKpisQuery>
{
    public GetOpsKpisQueryValidator()
    {
        When(x => x.Date.HasValue, () =>
        {
            RuleFor(x => x.Date!.Value.Year).InclusiveBetween(2000, 2100);
        });
    }
}
