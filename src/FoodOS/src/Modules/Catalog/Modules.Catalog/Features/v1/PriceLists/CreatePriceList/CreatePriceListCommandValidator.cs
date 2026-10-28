using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.CreatePriceList;

public sealed class CreatePriceListCommandValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ValidFrom).Must(v => v != default).WithMessage("ValidFrom is required.");
        RuleFor(x => x.ValidTo)
            .Must((cmd, to) => to is null || to >= cmd.ValidFrom)
            .WithMessage("ValidTo cannot be earlier than ValidFrom.");
        RuleFor(x => x.CustomerLevelId)
            .Must((cmd, levelId) => levelId is null || levelId == Guid.Empty || cmd.CustomerOrgId is null || cmd.CustomerOrgId == Guid.Empty)
            .WithMessage("A price list belongs to a customer or a level, not both.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.MinQty).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.Currency).NotEmpty().Length(3);
        }).When(x => x.Lines is not null);
    }
}
