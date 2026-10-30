using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists;

internal static class PriceListConflicts
{
    /// <summary>
    /// Two lists of the same kind with the same priority and overlapping validity that both price a product
    /// would make the quote depend on database order, so reject the second one with 409.
    /// </summary>
    public static async Task EnsureNoSamePriorityConflictAsync(
        CatalogDbContext dbContext,
        PriceList list,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(productIds);
        if (productIds.Count == 0)
        {
            return;
        }

        Guid listId = list.Id;
        Guid? orgId = list.CustomerOrgId;
        Guid? levelId = list.CustomerLevelId;
        int priority = list.Priority;
        DateTimeOffset from = list.ValidFrom;
        DateTimeOffset? to = list.ValidTo;

        string? conflict = await dbContext.PriceLists
            .AsNoTracking()
            .Where(other => other.Id != listId
                && other.CustomerOrgId == orgId
                && other.CustomerLevelId == levelId
                && other.Priority == priority
                && (other.ValidTo == null || other.ValidTo >= from)
                && (to == null || other.ValidFrom <= to)
                && dbContext.PriceListLines.Any(line => line.PriceListId == other.Id && productIds.Contains(line.ProductId)))
            .Select(other => other.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (conflict is not null)
        {
            throw new CustomException(
                $"Price list '{conflict}' has the same kind and priority {priority} and already prices this product; change one priority or keep the product in one list.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }
    }
}
