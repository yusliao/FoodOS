using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.QualityChecks;

public sealed record FailQualityCheckCommand(
    Guid PurchaseOrderId,
    Guid LineId,
    decimal Quantity,
    decimal SampleQty,
    string LotNo,
    DateOnly ExpiryDate,
    DateOnly? ManufacturedOn = null,
    string? Note = null,
    IReadOnlyList<Guid>? PhotoFileIds = null) : ICommand<Guid>;
