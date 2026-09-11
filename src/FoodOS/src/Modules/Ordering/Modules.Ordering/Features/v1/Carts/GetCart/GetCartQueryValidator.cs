using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Carts;

namespace FSH.Modules.Ordering.Features.v1.Carts.GetCart;

public sealed class GetCartQueryValidator : AbstractValidator<GetCartQuery>
{
    public GetCartQueryValidator()
    {
        RuleFor(x => x.StoreId).NotEmpty();
    }
}
