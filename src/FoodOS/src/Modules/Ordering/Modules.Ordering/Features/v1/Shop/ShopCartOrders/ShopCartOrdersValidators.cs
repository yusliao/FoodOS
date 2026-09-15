using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Shop;

namespace FSH.Modules.Ordering.Features.v1.Shop.ShopCartOrders;

public sealed class UpdateShopCartCommandValidator : AbstractValidator<UpdateShopCartCommand>
{
    public UpdateShopCartCommandValidator()
    {
        RuleFor(command => command.StoreId).NotEmpty();
        RuleFor(command => command.Lines).NotNull().Must(lines => lines.Count <= 200);
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(item => item.ProductId).NotEmpty();
            line.RuleFor(item => item.Quantity).GreaterThan(0);
        });
    }
}

public sealed class SearchShopOrdersQueryValidator : AbstractValidator<SearchShopOrdersQuery>
{
    public SearchShopOrdersQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class PlaceShopOrderCommandValidator : AbstractValidator<PlaceShopOrderCommand>
{
    public PlaceShopOrderCommandValidator() => RuleFor(command => command.StoreId).NotEmpty();
}

public sealed class AmendShopOrderCommandValidator : AbstractValidator<AmendShopOrderCommand>
{
    public AmendShopOrderCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.Lines).NotEmpty().Must(lines => lines.Count <= 200);
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(item => item.ProductId).NotEmpty();
            line.RuleFor(item => item.Quantity).GreaterThan(0);
        });
    }
}

public sealed class CancelShopOrderCommandValidator : AbstractValidator<CancelShopOrderCommand>
{
    public CancelShopOrderCommandValidator() => RuleFor(command => command.OrderId).NotEmpty();
}
