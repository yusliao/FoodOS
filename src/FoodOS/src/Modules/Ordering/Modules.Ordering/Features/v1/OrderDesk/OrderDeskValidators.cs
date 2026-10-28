using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.OrderDesk;

namespace FSH.Modules.Ordering.Features.v1.OrderDesk;

public sealed class SearchOrderDeskCallersQueryValidator : AbstractValidator<SearchOrderDeskCallersQuery>
{
    public SearchOrderDeskCallersQueryValidator()
    {
        RuleFor(query => query.Term).NotEmpty().MaximumLength(100);
        RuleFor(query => query.Limit).InclusiveBetween(1, 20);
    }
}

public sealed class SuggestOrderDeskProductsQueryValidator : AbstractValidator<SuggestOrderDeskProductsQuery>
{
    public SuggestOrderDeskProductsQueryValidator()
    {
        RuleFor(query => query.StoreId).NotEmpty();
        RuleFor(query => query.Term).NotEmpty().MaximumLength(100);
        RuleFor(query => query.Limit).InclusiveBetween(1, 20);
    }
}

public sealed class QuoteOrderDeskLinesQueryValidator : AbstractValidator<QuoteOrderDeskLinesQuery>
{
    public QuoteOrderDeskLinesQueryValidator()
    {
        RuleFor(query => query.StoreId).NotEmpty();
        RuleFor(query => query.Lines).SetValidator(new OrderDeskLinesValidator());
    }
}

public sealed class GetOrderDeskStoreHistoryQueryValidator : AbstractValidator<GetOrderDeskStoreHistoryQuery>
{
    public GetOrderDeskStoreHistoryQueryValidator()
    {
        RuleFor(query => query.StoreId).NotEmpty();
        RuleFor(query => query.OrderLimit).InclusiveBetween(1, 50);
        RuleFor(query => query.FrequentLimit).InclusiveBetween(1, 50);
    }
}

public sealed class PlaceOrderDeskOrderCommandValidator : AbstractValidator<PlaceOrderDeskOrderCommand>
{
    public PlaceOrderDeskOrderCommandValidator()
    {
        RuleFor(command => command.StoreId).NotEmpty();
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Lines).SetValidator(new OrderDeskLinesValidator());
    }
}

internal sealed class OrderDeskLinesValidator : AbstractValidator<IReadOnlyList<OrderDeskLineInput>>
{
    public const int MaxLines = 100;

    public OrderDeskLinesValidator()
    {
        RuleFor(lines => lines).NotNull()
            .Must(lines => lines.Count is > 0 and <= MaxLines)
            .WithMessage($"An order needs 1 to {MaxLines} lines.")
            .Must(lines => lines.Select(line => line.ProductId).Distinct().Count() == lines.Count)
            .WithMessage("Each product can only appear once per order.");
        RuleForEach(lines => lines).ChildRules(line =>
        {
            line.RuleFor(item => item.ProductId).NotEmpty();
            line.RuleFor(item => item.Quantity).GreaterThan(0).LessThanOrEqualTo(100_000);
        });
    }
}
