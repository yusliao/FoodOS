using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.ProofOfDelivery;

namespace FSH.Modules.Logistics.Features.v1.Pods.ConfirmPod;

public sealed class ConfirmPodCommandValidator : AbstractValidator<ConfirmPodCommand>
{
    public ConfirmPodCommandValidator()
    {
        RuleFor(x => x.StopId).NotEmpty();
        RuleFor(x => x.SignerName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Geo).MaximumLength(64);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.OrderLineId).NotEmpty();
            line.RuleFor(l => l.LotId).NotEmpty();
            line.RuleFor(l => l.SignedQty).GreaterThanOrEqualTo(0);
        });
    }
}
