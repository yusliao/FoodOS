using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

public sealed record PurchaseOrderLineInput(
    Guid ProductId,
    string Zone,
    decimal Quantity);

public sealed record CreatePurchaseOrderCommand(
    Guid SupplierId,
    Guid WarehouseId,
    DateTimeOffset ExpectedAt,
    IReadOnlyList<PurchaseOrderLineInput> Lines) : ICommand<Guid>;
