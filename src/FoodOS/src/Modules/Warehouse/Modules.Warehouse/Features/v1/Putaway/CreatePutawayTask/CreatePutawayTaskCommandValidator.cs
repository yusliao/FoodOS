using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;

namespace FSH.Modules.Warehouse.Features.v1.Putaway.CreatePutawayTask;

public sealed class CreatePutawayTaskCommandValidator : AbstractValidator<CreatePutawayTaskCommand>
{
    public CreatePutawayTaskCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Zone).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.LotId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Source).NotEmpty().MaximumLength(32);
    }
}
