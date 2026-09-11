using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Routes;

namespace FSH.Modules.Logistics.Features.v1.Routes.GetRouteById;

public sealed class GetRouteByIdQueryValidator : AbstractValidator<GetRouteByIdQuery>
{
    public GetRouteByIdQueryValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
    }
}
