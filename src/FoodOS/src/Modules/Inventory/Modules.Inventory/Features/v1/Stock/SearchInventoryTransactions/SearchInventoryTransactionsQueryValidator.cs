using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Stock;

namespace FSH.Modules.Inventory.Features.v1.Stock.SearchInventoryTransactions;

public sealed class SearchInventoryTransactionsQueryValidator : AbstractValidator<SearchInventoryTransactionsQuery>
{
    public SearchInventoryTransactionsQueryValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
