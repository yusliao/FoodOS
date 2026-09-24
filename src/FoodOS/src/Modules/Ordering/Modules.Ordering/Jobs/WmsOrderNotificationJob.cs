using System.Diagnostics;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Inventory.Contracts.v1.Warehouses;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.Ordering.Features.v1;
using FSH.Modules.WmsIntegration.Contracts.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Ordering.Jobs;

public sealed class WmsOrderNotificationJob(
    IMultiTenantStore<AppTenantInfo> tenantStore,
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    TimeProvider clock,
    ILogger<WmsOrderNotificationJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing")) return;
        var tenant = await tenantStore.GetAsync(MultitenancyConstants.Root.Id).ConfigureAwait(false);
        if (tenant is null || !tenant.IsActive)
        {
            logger.LogWarning("[Ordering] WMS order notification skipped because the operator tenant is unavailable");
            return;
        }

        try
        {
            int processed = await ProcessTenantAsync(tenant, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("[Ordering] WMS order notification processed {Count} order(s)", processed);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Ordering] WMS order notification job failed");
        }
    }

    public async Task<int> ProcessTenantAsync(
        AppTenantInfo tenant,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var gateway = scope.ServiceProvider.GetRequiredService<IWmsOrderNotificationGateway>();
        DateTimeOffset retryBefore = utcNow.AddSeconds(-30);
        var orders = await db.SalesOrders
            .Where(order => order.WarehouseConfirmationStatus == WarehouseConfirmationStatus.Pending
                && (order.WarehouseConfirmationUpdatedAt == null
                    || order.WarehouseConfirmationUpdatedAt <= retryBefore))
            .OrderBy(order => order.WarehouseConfirmationUpdatedAt)
            .ThenBy(order => order.Id)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int processed = 0;
        foreach (var order in orders)
        {
            try
            {
                WmsOrderNotificationResult result;
                string correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
                if (order.Status == SalesOrderStatus.Cancelled)
                {
                    result = await gateway.CancelAsync(
                            $"outbound:{order.Id:N}:cancel:{order.Revision}",
                            correlationId,
                            order.Id,
                            "Cancelled by FoodOS before cutoff.",
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    var store = await db.Stores.AsNoTracking()
                        .SingleAsync(item => item.Id == order.StoreId, cancellationToken)
                        .ConfigureAwait(false);
                    var warehouse = await mediator.Send(
                            new GetWarehouseByIdQuery(order.WarehouseId),
                            cancellationToken)
                        .ConfigureAwait(false);
                    var products = await ShopCatalog.GetActiveManyAsync(
                            mediator,
                            order.Lines.Select(line => line.ProductId),
                            cancellationToken)
                        .ConfigureAwait(false);
                    var lines = order.Lines.Select(line =>
                    {
                        var (product, _) = products[line.ProductId];
                        return new WmsOutboundOrderLine(
                            line.Id,
                            product.Sku,
                            product.BaseUom,
                            line.OrderedQty);
                    }).ToList();
                    result = await gateway.SubmitAsync(
                            $"outbound:{order.Id:N}:revision:{order.Revision}",
                            correlationId,
                            new WmsOutboundOrderNotice(
                                order.Id,
                                order.Number,
                                order.Revision,
                                warehouse.Code,
                                "root",
                                order.CutoffAt,
                                store.Id,
                                store.Name,
                                store.Address,
                                lines),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                if (result.Status is "accepted" or "completed")
                {
                    order.MarkWarehouseConfirmed(result.Detail ?? "Warehouse notification accepted.", utcNow);
                }
                else if (result.Status == "rejected")
                {
                    order.MarkWarehouseException(result.Detail ?? result.ErrorCode ?? "Warehouse rejected the notification.", utcNow);
                }
                else
                {
                    order.MarkWarehouseNotificationPending(
                        result.Detail ?? result.ErrorCode ?? "Warehouse notification is pending retry.",
                        utcNow);
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                order.MarkWarehouseNotificationPending(ex.Message, utcNow);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "[Ordering] WMS notification failed for order {OrderId}", order.Id);
            }
        }

        return processed;
    }
}
