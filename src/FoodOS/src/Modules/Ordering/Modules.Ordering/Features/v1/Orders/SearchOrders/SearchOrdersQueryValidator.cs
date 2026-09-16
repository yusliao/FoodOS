using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Domain;

namespace FSH.Modules.Ordering.Features.v1.Orders.SearchOrders;

public sealed class SearchOrdersQueryValidator : AbstractValidator<SearchOrdersQuery>
{
    public SearchOrdersQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Status).Must(status => status is null
            || Enum.GetNames<SalesOrderStatus>().Contains(status, StringComparer.OrdinalIgnoreCase));
    }
}
