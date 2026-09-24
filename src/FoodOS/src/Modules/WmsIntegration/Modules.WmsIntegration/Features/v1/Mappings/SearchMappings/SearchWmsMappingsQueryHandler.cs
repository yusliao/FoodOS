using FSH.Framework.Shared.Persistence;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
using FSH.Modules.WmsIntegration.Data;
using FSH.Modules.WmsIntegration.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Features.v1.Mappings.SearchMappings;

public sealed class SearchWmsMappingsQueryHandler(
    WmsIntegrationDbContext dbContext,
    IOptions<WmsIntegrationOptions> options)
    : IQueryHandler<SearchWmsMappingsQuery, PagedResponse<WmsMappingDto>>
{
    public async ValueTask<PagedResponse<WmsMappingDto>> Handle(
        SearchWmsMappingsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var settings = options.Value;
        IQueryable<WmsMapping> mappings = dbContext.Mappings.AsNoTracking()
            .Where(x => x.Provider == settings.Provider && x.ConnectionId == settings.ConnectionId);

        if (!string.IsNullOrWhiteSpace(query.Kind))
        {
            string kind = WmsMappingKinds.All.Single(candidate =>
                string.Equals(candidate, query.Kind.Trim(), StringComparison.OrdinalIgnoreCase));
            mappings = mappings.Where(x => x.Kind == kind);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            mappings = mappings.Where(x => EF.Functions.ILike(x.FoodOsValue, $"%{search}%")
                || EF.Functions.ILike(x.ExternalValue, $"%{search}%"));
        }
        if (query.IsActive.HasValue)
        {
            mappings = mappings.Where(x => x.IsActive == query.IsActive.Value);
        }

        long total = await mappings.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await mappings
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.FoodOsValue)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResponse<WmsMappingDto>
        {
            Items = items.Select(x => x.ToDto()).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.PageSize),
        };
    }
}
