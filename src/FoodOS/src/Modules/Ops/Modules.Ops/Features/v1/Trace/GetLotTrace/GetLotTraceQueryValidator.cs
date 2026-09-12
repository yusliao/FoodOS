using FluentValidation;
using FSH.Modules.Ops.Contracts.v1.Trace;

namespace FSH.Modules.Ops.Features.v1.Trace.GetLotTrace;

public sealed class GetLotTraceQueryValidator : AbstractValidator<GetLotTraceQuery>
{
    public GetLotTraceQueryValidator()
    {
        RuleFor(x => x.LotId).NotEmpty();
    }
}
