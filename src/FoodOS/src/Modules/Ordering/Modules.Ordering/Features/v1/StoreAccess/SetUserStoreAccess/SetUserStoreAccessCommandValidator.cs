using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.StoreAccess;

namespace FSH.Modules.Ordering.Features.v1.StoreAccess.SetUserStoreAccess;

public sealed class SetUserStoreAccessCommandValidator : AbstractValidator<SetUserStoreAccessCommand>
{
    public SetUserStoreAccessCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.StoreIds).NotNull();
    }
}
