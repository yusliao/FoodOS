using System.Globalization;
using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Plans;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using FSH.Modules.Ordering.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Services;

/// <summary>
/// Creates a platform-committed order for a store: resolves the business day and cutoff from the
/// operator warehouse, prices lines with the customer's contract prices and records the placement.
/// No local stock is reserved; the WMS notification job picks the order up asynchronously.
/// Shared by Shop (customer) and order desk (operator on the customer's behalf) placement.
/// </summary>
public sealed class PlatformOrderPlacement(OrderingDbContext dbContext, IMediator mediator, TimeProvider clock)
{
    public Task<SalesOrder?> FindByIdempotencyKeyAsync(
        string? customerTenantId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.SalesOrders.AsNoTracking()
            .SingleOrDefaultAsync(order =>
                order.CustomerTenantId == customerTenantId
                && order.PlacementIdempotencyKey == idempotencyKey,
                cancellationToken);

    /// <summary>Checks that the store and its customer can take orders; returns the customer organization.</summary>
    public async Task<CustomerOrg> RequireOrderableAsync(Store store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (string.IsNullOrWhiteSpace(store.Address))
        {
            throw Conflict("The store delivery address must be completed before placing an order.");
        }

        var org = await dbContext.CustomerOrgs
            .FirstOrDefaultAsync(item => item.Id == store.CustomerOrgId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Customer organization {store.CustomerOrgId} not found.");
        if (org.CreditHold)
        {
            throw Conflict("Customer is on credit hold.");
        }

        return org;
    }

    /// <summary>Adds the placed order to the context; the caller saves it together with its own changes.</summary>
    public async Task<SalesOrder> AddPlacedOrderAsync(
        Store store,
        CustomerOrg org,
        IReadOnlyList<(Guid ProductId, decimal Quantity)> lines,
        string idempotencyKey,
        SalesOrderPlacementChannel channel,
        Guid? placedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(org);
        ArgumentNullException.ThrowIfNull(lines);
        var warehouse = await StoreWarehouseResolver.ResolveAsync(mediator, store.DefaultWarehouseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw Conflict("The operator warehouse must be configured before placing an order.");
        DateTimeOffset utcNow = clock.GetUtcNow();
        TimeOnly cutoffLocal = TimeOnly.ParseExact(
            warehouse.Clock.CutoffLocal,
            "HH:mm",
            CultureInfo.InvariantCulture);
        var (businessDate, cutoffAt) = OperatingCutoff.Resolve(warehouse.Clock.TimeZoneId, cutoffLocal, utcNow);
        for (int i = 0; i < 7; i++)
        {
            var existingPlan = await mediator
                .Send(new GetDailyPlanQuery(warehouse.Id, businessDate), cancellationToken)
                .ConfigureAwait(false);
            if (existingPlan is null)
            {
                break;
            }

            (businessDate, cutoffAt) = OperatingCutoff.NextAfter(
                warehouse.Clock.TimeZoneId,
                cutoffLocal,
                businessDate);
        }

        var products = await ShopCatalog.GetActiveManyAsync(
                mediator,
                lines.Select(line => line.ProductId),
                cancellationToken)
            .ConfigureAwait(false);
        var quotes = await ShopCatalog.QuoteManyAsync(mediator, org.Id, lines, cancellationToken)
            .ConfigureAwait(false);
        var draftLines = new List<SalesOrderLineDraft>();
        foreach (var (productId, quantity) in lines)
        {
            if (!products.TryGetValue(productId, out var product))
            {
                throw new NotFoundException($"Product {productId} not found.");
            }

            var quote = quotes[productId];
            draftLines.Add(SalesOrderLineDraft.FromQuote(productId, product.Zone.ToString(), quantity, quote));
        }

        string number = await OrderNumbers.NextAsync(dbContext, businessDate, cancellationToken).ConfigureAwait(false);
        var order = SalesOrder.CreateDraft(
            number,
            store.Id,
            org.Id,
            warehouse.Id,
            businessDate,
            cutoffAt,
            draftLines,
            store.CustomerTenantId);
        order.PlaceWithPlatformCommitment(idempotencyKey, utcNow, channel, placedByUserId);
        dbContext.SalesOrders.Add(order);
        return order;
    }

    public static CustomException Conflict(string message) => new(
        message,
        (IEnumerable<string>?)null,
        HttpStatusCode.Conflict);
}
