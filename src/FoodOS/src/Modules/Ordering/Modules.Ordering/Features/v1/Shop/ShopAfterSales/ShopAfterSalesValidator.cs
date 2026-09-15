using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Shop;

namespace FSH.Modules.Ordering.Features.v1.Shop.ShopAfterSales;

public sealed class CreateShopAfterSalesCommandValidator : AbstractValidator<CreateShopAfterSalesCommand>
{
    public CreateShopAfterSalesCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.OrderLineId).NotEmpty();
        RuleFor(command => command.Type).NotEmpty();
        RuleFor(command => command.Quantity).GreaterThan(0);
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(256);
    }
}
