using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Stores;

namespace FSH.Modules.Ordering.Features.v1.Stores.GetStoreById;

public sealed class GetStoreByIdQueryValidator : AbstractValidator<GetStoreByIdQuery>
{
    public GetStoreByIdQueryValidator()
    {
        RuleFor(x => x.StoreId).NotEmpty();
    }
}
