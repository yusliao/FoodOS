using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Stores;

namespace FSH.Modules.Ordering.Features.v1.Stores.CreateStore;

public sealed class CreateStoreCommandValidator : AbstractValidator<CreateStoreCommand>
{
    public CreateStoreCommandValidator()
    {
        RuleFor(x => x.CustomerOrgId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DefaultWarehouseId).NotEmpty();
        RuleFor(x => x.DeliveryWindow).MaximumLength(64);
    }
}
