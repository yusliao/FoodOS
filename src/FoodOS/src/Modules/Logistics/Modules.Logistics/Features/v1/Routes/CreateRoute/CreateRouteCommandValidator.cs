using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Routes;

namespace FSH.Modules.Logistics.Features.v1.Routes.CreateRoute;

public sealed class CreateRouteCommandValidator : AbstractValidator<CreateRouteCommand>
{
    public CreateRouteCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.StoreIds).NotEmpty();
        RuleForEach(x => x.StoreIds).NotEmpty();
    }
}
