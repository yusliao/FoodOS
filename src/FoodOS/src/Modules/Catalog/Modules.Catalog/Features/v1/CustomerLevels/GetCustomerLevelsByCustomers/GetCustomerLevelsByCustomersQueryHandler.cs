using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using FSH.Modules.Catalog.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.GetCustomerLevelsByCustomers;

public sealed class GetCustomerLevelsByCustomersQueryHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetCustomerLevelsByCustomersQuery, IReadOnlyList<CustomerLevelOfCustomerDto>>
{
    public async ValueTask<IReadOnlyList<CustomerLevelOfCustomerDto>> Handle(
        GetCustomerLevelsByCustomersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var ids = query.CustomerOrgIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.CustomerLevelAssignments
            .AsNoTracking()
            .Where(item => ids.Contains(item.CustomerOrgId))
            .Join(
                dbContext.CustomerLevels.AsNoTracking(),
                item => item.CustomerLevelId,
                level => level.Id,
                (item, level) => new CustomerLevelOfCustomerDto(item.CustomerOrgId, level.Id, level.Code, level.Name, level.DiscountPercent))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
