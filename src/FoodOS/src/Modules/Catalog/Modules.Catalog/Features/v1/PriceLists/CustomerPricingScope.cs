using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists;

/// <summary>Loads everything a quote for one customer may use: its level and the price lists it can see.</summary>
internal static class CustomerPricingScope
{
    public static async Task<(CustomerLevel? Level, List<PriceList> Lists)> LoadAsync(
        CatalogDbContext dbContext,
        Guid customerOrgId,
        CancellationToken cancellationToken)
    {
        var level = await dbContext.CustomerLevelAssignments
            .AsNoTracking()
            .Where(item => item.CustomerOrgId == customerOrgId)
            .Join(dbContext.CustomerLevels.AsNoTracking(), item => item.CustomerLevelId, item => item.Id, (_, item) => item)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid? levelId = level?.Id;

        // 只取本客户专属、本等级、全局三类价目表，别的客户/等级的价格一张都不能带出来
        var lists = await dbContext.PriceLists
            .AsNoTracking()
            .Where(list => list.CustomerOrgId == customerOrgId
                || (list.CustomerOrgId == null && (list.CustomerLevelId == null || list.CustomerLevelId == levelId)))
            .Include(list => list.Lines)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (level, lists);
    }
}
