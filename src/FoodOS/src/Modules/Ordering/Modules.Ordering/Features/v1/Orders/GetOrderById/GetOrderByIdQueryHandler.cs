using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Orders.GetOrderById;

/// <summary>Operator order detail; resolves store, customer, warehouse and product names so staff do not read raw ids.</summary>
public sealed class GetOrderByIdQueryHandler(OrderingDbContext dbContext, IMediator mediator)
    : IQueryHandler<GetOrderByIdQuery, SalesOrderDto>
{
    public async ValueTask<SalesOrderDto> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var order = await dbContext.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == query.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Order {query.OrderId} not found.");

        var store = await dbContext.Stores.AsNoTracking()
            .Where(s => s.Id == order.StoreId)
            .Select(s => new { s.Code, s.Name })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var customer = await dbContext.CustomerOrgs.AsNoTracking()
            .Where(c => c.Id == order.CustomerOrgId)
            .Select(c => new { c.Code, c.Name })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var warehouse = await FindWarehouseAsync(order.WarehouseId, cancellationToken).ConfigureAwait(false);

        var productIds = order.Lines.Select(line => line.ProductId).Distinct().ToList();
        var products = productIds.Count == 0
            ? []
            : (await mediator.Send(new GetProductsByIdsQuery(productIds), cancellationToken).ConfigureAwait(false))
                .ToDictionary(product => product.Id);

        var dto = order.ToDto();
        return dto with
        {
            StoreCode = store?.Code,
            StoreName = store?.Name,
            CustomerOrgCode = customer?.Code,
            CustomerOrgName = customer?.Name,
            WarehouseCode = warehouse?.Code,
            WarehouseName = warehouse?.Name,
            Lines = dto.Lines.Select(line => products.TryGetValue(line.ProductId, out var product)
                ? line with { Sku = product.Sku, ProductName = product.Name, BaseUom = product.BaseUom }
                : line).ToList(),
        };
    }

    // 仓库被删了也不能让订单详情打不开，查不到就只显示 ID
    private async Task<WarehouseDto?> FindWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        if (warehouseId == Guid.Empty)
        {
            return null;
        }

        try
        {
            return await mediator.Send(new GetWarehouseByIdQuery(warehouseId), cancellationToken).ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }
}
