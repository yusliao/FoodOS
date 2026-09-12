using FluentValidation;
using FSH.Modules.Warehouse.Contracts.v1.Trace;

namespace FSH.Modules.Warehouse.Features.v1.Trace.ListLotTraceEvents;

public sealed class ListLotTraceEventsQueryValidator : AbstractValidator<ListLotTraceEventsQuery>
{
    public ListLotTraceEventsQueryValidator()
    {
        RuleFor(x => x.LotId).NotEmpty();
    }
}
