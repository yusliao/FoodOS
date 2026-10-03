using FluentValidation;
using FSH.Modules.WmsIntegration.Contracts.v1;

namespace FSH.Modules.WmsIntegration.Features.v1.GetAvailability;

public sealed class GetWmsAvailabilityQueryValidator : AbstractValidator<GetWmsAvailabilityQuery>
{
    public GetWmsAvailabilityQueryValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Uom).NotEmpty().MaximumLength(32);
    }
}
