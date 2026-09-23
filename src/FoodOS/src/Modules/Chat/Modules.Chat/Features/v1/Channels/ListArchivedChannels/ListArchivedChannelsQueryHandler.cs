using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Chat.Contracts.v1.DTOs;
using FSH.Modules.Chat.Contracts.v1.Queries;
using FSH.Modules.Chat.Data;
using FSH.Modules.Chat.Features.v1.Internal;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Chat.Features.v1.Channels.ListArchivedChannels;

public sealed class ListArchivedChannelsQueryHandler(ChatDbContext db, ICurrentUser currentUser)
    : IQueryHandler<ListArchivedChannelsQuery, PagedResponse<ChannelDto>>
{
    public async ValueTask<PagedResponse<ChannelDto>> Handle(
        ListArchivedChannelsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var tenantId = currentUser.GetTenant();
        if (string.IsNullOrWhiteSpace(tenantId)) throw new UnauthorizedException("no current tenant");

        var channels = db.Channels
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(channel => channel.IsDeleted
                && EF.Property<string>(channel, "TenantId") == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            channels = channels.Where(channel =>
                (channel.Name != null && EF.Functions.ILike(channel.Name, $"%{search}%"))
                || (channel.Slug != null && EF.Functions.ILike(channel.Slug, $"%{search}%")));
        }

        long total = await channels.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await channels
            .OrderByDescending(channel => channel.DeletedOnUtc)
            .ThenByDescending(channel => channel.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResponse<ChannelDto>
        {
            Items = rows.Select(channel => channel.ToDto()).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.PageSize),
        };
    }
}
