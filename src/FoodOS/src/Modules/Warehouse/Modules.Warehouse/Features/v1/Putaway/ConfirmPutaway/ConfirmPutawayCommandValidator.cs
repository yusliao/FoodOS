using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;

namespace FSH.Modules.Warehouse.Features.v1.Putaway.ConfirmPutaway;

public sealed class ConfirmPutawayCommandValidator : AbstractValidator<ConfirmPutawayCommand>
{
    public ConfirmPutawayCommandValidator()
    {
        RuleFor(x => x.PutawayTaskId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
    }
}
