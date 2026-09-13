using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;

namespace FSH.Modules.Warehouse.Features.v1.Putaway.SearchPutawayTasks;

public sealed class SearchPutawayTasksQueryValidator : AbstractValidator<SearchPutawayTasksQuery>
{
    public SearchPutawayTasksQueryValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Status).MaximumLength(16);
    }
}
