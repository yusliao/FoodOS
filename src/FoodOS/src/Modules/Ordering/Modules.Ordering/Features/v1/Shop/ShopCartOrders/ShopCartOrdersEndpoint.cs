using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Web.Idempotency;
using FSH.Modules.Ordering.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Ordering.Features.v1.Shop.ShopCartOrders;

public static class ShopCartOrderEndpoints
{
    internal static void MapShopCartOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGetShopCartEndpoint();
        endpoints.MapUpdateShopCartEndpoint();
        endpoints.MapSearchShopOrdersEndpoint();
        endpoints.MapGetShopOrderByIdEndpoint();
        endpoints.MapPlaceShopOrderEndpoint();
        endpoints.MapAmendShopOrderEndpoint();
        endpoints.MapCancelShopOrderEndpoint();
    }
}

public static class GetShopCartEndpoint
{
    internal static RouteHandlerBuilder MapGetShopCartEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/stores/{storeId:guid}/cart",
                (Guid storeId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetShopCartQuery(storeId), ct))
            .WithName("GetShopCart")
            .RequirePermission(OrderingPermissions.Shop.Order);
}

public static class UpdateShopCartEndpoint
{
    internal static RouteHandlerBuilder MapUpdateShopCartEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPut("/stores/{storeId:guid}/cart",
                (Guid storeId, UpdateShopCartRequest request, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new UpdateShopCartCommand(storeId, request.Lines), ct))
            .WithName("UpdateShopCart")
            .RequirePermission(OrderingPermissions.Shop.Order);
}

public static class SearchShopOrdersEndpoint
{
    internal static RouteHandlerBuilder MapSearchShopOrdersEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/orders",
                (Guid? storeId, int? pageNumber, int? pageSize, string? status, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SearchShopOrdersQuery(
                        storeId,
                        pageNumber is null or 0 ? 1 : pageNumber.Value,
                        pageSize is null or 0 ? 20 : pageSize.Value, status), ct))
            .WithName("SearchShopOrders")
            .WithSummary("Search authorized store orders, including Received and Reconciled; not a payment statement")
            .RequirePermission(OrderingPermissions.Shop.View);
}

public static class GetShopOrderByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetShopOrderByIdEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/orders/{orderId:guid}",
                (Guid orderId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetShopOrderByIdQuery(orderId), ct))
            .WithName("GetShopOrderById")
            .RequirePermission(OrderingPermissions.Shop.View);
}

public static class PlaceShopOrderEndpoint
{
    internal static RouteHandlerBuilder MapPlaceShopOrderEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/orders",
                (PlaceShopOrderRequest request, HttpRequest httpRequest, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new PlaceShopOrderCommand(
                        request.StoreId,
                        httpRequest.Headers["Idempotency-Key"].ToString()), ct))
            .WithName("PlaceShopOrder")
            .RequirePermission(OrderingPermissions.Shop.Order)
            .WithIdempotency();
}

public static class AmendShopOrderEndpoint
{
    internal static RouteHandlerBuilder MapAmendShopOrderEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/orders/{orderId:guid}/amend",
                (Guid orderId, AmendShopOrderRequest request, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new AmendShopOrderCommand(orderId, request.Lines), ct))
            .WithName("AmendShopOrder")
            .RequirePermission(OrderingPermissions.Shop.Order)
            .WithIdempotency();
}

public static class CancelShopOrderEndpoint
{
    internal static RouteHandlerBuilder MapCancelShopOrderEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/orders/{orderId:guid}/cancel",
                (Guid orderId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new CancelShopOrderCommand(orderId), ct))
            .WithName("CancelShopOrder")
            .RequirePermission(OrderingPermissions.Shop.Order)
            .WithIdempotency();
}

internal sealed record UpdateShopCartRequest(IReadOnlyList<CartLineInput> Lines);
internal sealed record PlaceShopOrderRequest(Guid StoreId);
internal sealed record AmendShopOrderRequest(IReadOnlyList<AmendOrderLineInput> Lines);
