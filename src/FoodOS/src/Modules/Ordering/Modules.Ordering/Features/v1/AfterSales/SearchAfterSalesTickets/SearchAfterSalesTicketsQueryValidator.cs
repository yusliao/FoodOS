using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.AfterSales;

namespace FSH.Modules.Ordering.Features.v1.AfterSales.SearchAfterSalesTickets;

public sealed class SearchAfterSalesTicketsQueryValidator : AbstractValidator<SearchAfterSalesTicketsQuery>
{
    public SearchAfterSalesTicketsQueryValidator()
    {
        RuleFor(x => x.StoreId).NotEmpty();
    }
}
