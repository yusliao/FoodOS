using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

public sealed record SendPurchaseOrderCommand(Guid PurchaseOrderId) : ICommand<Guid>;
