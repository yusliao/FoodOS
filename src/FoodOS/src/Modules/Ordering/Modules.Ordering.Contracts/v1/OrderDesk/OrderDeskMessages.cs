using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.OrderDesk;

/// <summary>
/// Finds stores for an incoming call. A term with at least four digits matches contact phone numbers;
/// any term also matches store and customer names and codes.
/// </summary>
public sealed record SearchOrderDeskCallersQuery(string Term, int Limit = 10) : IQuery<IReadOnlyList<OrderDeskCallerDto>>;

/// <summary>Stores the current agent placed service orders for in the last 90 days, most frequent first.</summary>
public sealed record GetMyOrderDeskCallersQuery(int Limit = 12) : IQuery<IReadOnlyList<OrderDeskFrequentCallerDto>>;

public sealed record SuggestOrderDeskProductsQuery(Guid StoreId, string Term, int Limit = 10)
    : IQuery<IReadOnlyList<OrderDeskProductDto>>;

public sealed record OrderDeskLineInput(Guid ProductId, decimal Quantity);

/// <summary>Prices the lines with the store's contract prices at the requested quantities.</summary>
public sealed record QuoteOrderDeskLinesQuery(Guid StoreId, IReadOnlyList<OrderDeskLineInput> Lines)
    : IQuery<IReadOnlyList<OrderDeskQuoteLineDto>>;

/// <summary>Recent orders (with lines) and frequently ordered products of a store.</summary>
public sealed record GetOrderDeskStoreHistoryQuery(Guid StoreId, int OrderLimit = 10, int FrequentLimit = 20)
    : IQuery<OrderDeskStoreHistoryDto>;

/// <summary>
/// Places an order for the store on the customer's behalf. Like Shop placement it is a platform
/// commitment: no local stock is reserved and the warehouse is notified asynchronously.
/// </summary>
public sealed record PlaceOrderDeskOrderCommand(
    Guid StoreId,
    IReadOnlyList<OrderDeskLineInput> Lines,
    string IdempotencyKey) : ICommand<Guid>;
