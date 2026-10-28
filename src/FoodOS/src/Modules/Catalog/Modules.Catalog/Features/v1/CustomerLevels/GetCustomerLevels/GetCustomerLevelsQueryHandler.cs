using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using FSH.Modules.Catalog.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.GetCustomerLevels;

public sealed class GetCustomerLevelsQueryHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetCustomerLevelsQuery, IReadOnlyList<CustomerLevelDto>>
{
    public async ValueTask<IReadOnlyList<CustomerLevelDto>> Handle(
        GetCustomerLevelsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await dbContext.CustomerLevels
            .AsNoTracking()
            .OrderByDescending(level => level.DiscountPercent)
            .ThenBy(level => level.Code)
            .Select(level => new CustomerLevelDto(
                level.Id,
                level.Code,
                level.Name,
                level.DiscountPercent,
                dbContext.CustomerLevelAssignments.Count(item => item.CustomerLevelId == level.Id)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
