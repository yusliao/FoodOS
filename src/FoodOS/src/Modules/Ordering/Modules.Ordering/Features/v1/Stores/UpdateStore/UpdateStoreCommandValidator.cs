using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Stores;

namespace FSH.Modules.Ordering.Features.v1.Stores.UpdateStore;

public sealed class UpdateStoreCommandValidator : AbstractValidator<UpdateStoreCommand>
{
    public UpdateStoreCommandValidator()
    {
        RuleFor(command => command.StoreId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(128);
        RuleFor(command => command.Address).NotEmpty().MaximumLength(256);
    }
}
