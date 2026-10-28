using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.CustomerLevels;
using FSH.Modules.Catalog.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels.GetCustomerLevelAssignments;

public sealed class GetCustomerLevelAssignmentsQueryHandler(CatalogDbContext dbContext)
    : IQueryHandler<GetCustomerLevelAssignmentsQuery, IReadOnlyList<CustomerLevelAssignmentDto>>
{
    public async ValueTask<IReadOnlyList<CustomerLevelAssignmentDto>> Handle(
        GetCustomerLevelAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await dbContext.CustomerLevelAssignments
            .AsNoTracking()
            .Select(item => new CustomerLevelAssignmentDto(item.CustomerOrgId, item.CustomerLevelId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
