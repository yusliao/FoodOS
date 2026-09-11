using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

public sealed record SearchPurchaseOrdersQuery(string? Search = null) : IQuery<IReadOnlyList<PurchaseOrderDto>>;
