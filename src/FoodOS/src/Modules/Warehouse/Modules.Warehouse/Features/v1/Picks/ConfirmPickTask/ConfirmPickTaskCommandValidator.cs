using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Picks;

namespace FSH.Modules.Warehouse.Features.v1.Picks.ConfirmPickTask;

public sealed class ConfirmPickTaskCommandValidator : AbstractValidator<ConfirmPickTaskCommand>
{
    public ConfirmPickTaskCommandValidator()
    {
        RuleFor(x => x.PickTaskId).NotEmpty();
        RuleFor(x => x.ScannedLotId).NotEmpty();
    }
}
