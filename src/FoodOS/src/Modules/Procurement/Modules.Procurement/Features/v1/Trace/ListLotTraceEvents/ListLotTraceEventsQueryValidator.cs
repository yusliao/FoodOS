using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.Trace;

namespace FSH.Modules.Procurement.Features.v1.Trace.ListLotTraceEvents;

public sealed class ListLotTraceEventsQueryValidator : AbstractValidator<ListLotTraceEventsQuery>
{
    public ListLotTraceEventsQueryValidator()
    {
        RuleFor(x => x.LotId).NotEmpty();
    }
}
