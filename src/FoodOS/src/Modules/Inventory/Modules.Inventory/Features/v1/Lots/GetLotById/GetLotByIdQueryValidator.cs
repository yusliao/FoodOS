using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Lots;

namespace FSH.Modules.Inventory.Features.v1.Lots.GetLotById;

public sealed class GetLotByIdQueryValidator : AbstractValidator<GetLotByIdQuery>
{
    public GetLotByIdQueryValidator()
    {
        RuleFor(x => x.LotId).NotEmpty();
    }
}
