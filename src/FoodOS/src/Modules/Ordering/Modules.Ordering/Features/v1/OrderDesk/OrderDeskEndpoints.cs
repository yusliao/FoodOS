using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.v1.OrderDesk;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.OrderDesk;

public static class OrderDeskEndpoints
{
    internal static void MapOrderDeskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/callers",
                (string term, int? limit, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchOrderDeskCallersQuery(term, limit ?? 10), ct))
            .WithName("SearchOrderDeskCallers")
            .WithSummary("Find the calling restaurant store by phone number, name or code")
            .RequirePermission(OrderingPermissions.OrderDesk.View);

        endpoints.MapGet("/stores/{storeId:guid}/products",
                (Guid storeId, string term, int? limit, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SuggestOrderDeskProductsQuery(storeId, term, limit ?? 10), ct))
            .WithName("SuggestOrderDeskProducts")
            .WithSummary("Type-ahead product candidates with the store's contract price and availability")
            .RequirePermission(OrderingPermissions.OrderDesk.View);

        endpoints.MapPost("/stores/{storeId:guid}/quote",
                (Guid storeId, OrderDeskLinesRequest request, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new QuoteOrderDeskLinesQuery(storeId, request.Lines), ct))
            .WithName("QuoteOrderDeskLines")
            .WithSummary("Price order lines with the store's contract prices")
            .RequirePermission(OrderingPermissions.OrderDesk.View);

        endpoints.MapGet("/stores/{storeId:guid}/history",
                (Guid storeId, int? orderLimit, int? frequentLimit, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetOrderDeskStoreHistoryQuery(storeId, orderLimit ?? 10, frequentLimit ?? 20), ct))
            .WithName("GetOrderDeskStoreHistory")
            .WithSummary("Recent orders with lines and frequently ordered products of a store")
            .RequirePermission(OrderingPermissions.OrderDesk.View);

        endpoints.MapPost("/stores/{storeId:guid}/orders",
                (Guid storeId, OrderDeskLinesRequest request, HttpRequest httpRequest, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new PlaceOrderDeskOrderCommand(
                        storeId,
                        request.Lines,
                        httpRequest.Headers["Idempotency-Key"].ToString()), ct))
            .WithName("PlaceOrderDeskOrder")
            .WithSummary("Place an order for the store on the restaurant's behalf")
            .RequirePermission(OrderingPermissions.OrderDesk.Place)
            .WithIdempotency();
    }
}

internal sealed record OrderDeskLinesRequest(IReadOnlyList<OrderDeskLineInput> Lines);
