using FSH.Framework.Core.Exceptions;
using FSH.Modules.Logistics.Contracts.Dtos;
using FSH.Modules.Logistics.Contracts.v1.Routes;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Routes.GetRouteById;

public sealed class GetRouteByIdQueryHandler(LogisticsDbContext dbContext)
    : IQueryHandler<GetRouteByIdQuery, RouteDto>
{
    public async ValueTask<RouteDto> Handle(GetRouteByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var route = await dbContext.Routes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == query.RouteId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Route {query.RouteId} not found.");
        return route.ToDto();
    }
}
