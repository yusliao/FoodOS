using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.QualityChecks;

namespace FSH.Modules.Procurement.Features.v1.QualityChecks.FailQualityCheck;

public sealed class FailQualityCheckCommandValidator : AbstractValidator<FailQualityCheckCommand>
{
    public FailQualityCheckCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.SampleQty).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LotNo).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ExpiryDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Note).MaximumLength(512);
    }
}
