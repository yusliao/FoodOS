using FSH.Framework.Core.Context;
using FSH.Modules.Procurement.Contracts.v1.QualityChecks;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Procurement.Features.v1.QualityChecks;
using Mediator;

namespace FSH.Modules.Procurement.Features.v1.QualityChecks.PassQualityCheck;

public sealed class PassQualityCheckCommandHandler(
    ProcurementDbContext dbContext,
    IMediator mediator,
    ICurrentUser currentUser)
    : ICommandHandler<PassQualityCheckCommand, Guid>
{
    public ValueTask<Guid> Handle(PassQualityCheckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new ValueTask<Guid>(QualityCheckRecording.RecordAsync(
            dbContext,
            mediator,
            currentUser,
            command.PurchaseOrderId,
            command.LineId,
            QualityCheckResult.Pass,
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
