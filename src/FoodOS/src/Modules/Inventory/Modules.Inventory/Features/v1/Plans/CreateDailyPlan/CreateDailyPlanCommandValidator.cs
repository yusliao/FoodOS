using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Plans;

namespace FSH.Modules.Inventory.Features.v1.Plans.CreateDailyPlan;

public sealed class CreateDailyPlanCommandValidator : AbstractValidator<CreateDailyPlanCommand>
{
    public CreateDailyPlanCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        When(x => x.BusinessDate.HasValue, () =>
        {
            RuleFor(x => x.BusinessDate!.Value.Year).InclusiveBetween(2000, 2100);
        });
    }
}
