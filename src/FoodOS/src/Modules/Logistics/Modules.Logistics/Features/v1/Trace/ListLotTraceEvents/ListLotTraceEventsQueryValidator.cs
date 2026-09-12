using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Trace;

namespace FSH.Modules.Logistics.Features.v1.Trace.ListLotTraceEvents;

public sealed class ListLotTraceEventsQueryValidator : AbstractValidator<ListLotTraceEventsQuery>
{
    public ListLotTraceEventsQueryValidator()
    {
        RuleFor(x => x.LotId).NotEmpty();
    }
}
