using FSH.Framework.Core.Context;
using FSH.Modules.Procurement.Contracts.v1.QualityChecks;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Procurement.Features.v1.QualityChecks;
using Mediator;

namespace FSH.Modules.Procurement.Features.v1.QualityChecks.FailQualityCheck;

public sealed class FailQualityCheckCommandHandler(
    ProcurementDbContext dbContext,
    IMediator mediator,
    ICurrentUser currentUser)
    : ICommandHandler<FailQualityCheckCommand, Guid>
{
    public ValueTask<Guid> Handle(FailQualityCheckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new ValueTask<Guid>(QualityCheckRecording.RecordAsync(
            dbContext,
            mediator,
            currentUser,
            command.PurchaseOrderId,
            command.LineId,
            QualityCheckResult.Fail,
            command.Quantity,
            command.SampleQty,
            command.LotNo,
            command.ExpiryDate,
            command.ManufacturedOn,
            command.Note,
            command.PhotoFileIds,
            cancellationToken));
    }
}
