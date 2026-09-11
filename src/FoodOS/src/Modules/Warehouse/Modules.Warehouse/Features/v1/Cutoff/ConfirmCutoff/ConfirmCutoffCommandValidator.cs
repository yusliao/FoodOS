using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Cutoff;

namespace FSH.Modules.Warehouse.Features.v1.Cutoff.ConfirmCutoff;

public sealed class ConfirmCutoffCommandValidator : AbstractValidator<ConfirmCutoffCommand>
{
    public ConfirmCutoffCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}
