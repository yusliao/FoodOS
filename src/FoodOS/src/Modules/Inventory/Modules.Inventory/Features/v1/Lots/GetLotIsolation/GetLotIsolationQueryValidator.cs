using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Lots;

namespace FSH.Modules.Inventory.Features.v1.Lots.GetLotIsolation;

public sealed class GetLotIsolationQueryValidator : AbstractValidator<GetLotIsolationQuery>
{
    public GetLotIsolationQueryValidator()
    {
        RuleFor(x => x.LotId).NotEmpty();
    }
}
