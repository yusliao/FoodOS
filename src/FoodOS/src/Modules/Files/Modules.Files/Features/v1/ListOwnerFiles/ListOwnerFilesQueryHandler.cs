using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Files.Contracts;
using FSH.Modules.Files.Contracts.v1.DTOs;
using FSH.Modules.Files.Contracts.v1.Queries;
using FSH.Modules.Files.Data;
using FSH.Modules.Files.Domain;
using FSH.Modules.Files.Features.v1.Internal;
using FSH.Modules.Files.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Files.Features.v1.ListOwnerFiles;

public sealed class ListOwnerFilesQueryHandler(
    FilesDbContext db, FileAccessPolicyRegistry policies, ICurrentUser currentUser)
    : IQueryHandler<ListOwnerFilesQuery, PagedResponse<FileAssetDto>>
{
    public async ValueTask<PagedResponse<FileAssetDto>> Handle(ListOwnerFilesQuery q, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(q);
        if (!currentUser.IsAuthenticated() || currentUser.GetUserId() == Guid.Empty
            || string.IsNullOrWhiteSpace(currentUser.GetTenant())
            || policies.Resolve(q.OwnerType) is not IFileOwnerListPolicy policy)
            throw new NotFoundException("file owner not found");

        var tenantIds = await policy.GetReadTenantIdsAsync(q.OwnerId, currentUser.GetUserId().ToString(), cancellationToken)
            .ConfigureAwait(false);
        if (tenantIds is null || tenantIds.Count == 0)
            throw new NotFoundException("file owner not found");

        string ownerPattern = policy.OwnerType.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        // Cross-domain listing is opt-in and authorized before counting. Replace the tenant
        // filter only with the policy's source domains; retain explicit soft-delete/status/privacy bounds.
        var files = db.FileAssets.IgnoreQueryFilters().AsNoTracking()
            .Where(f => !f.IsDeleted && f.OwnerId == q.OwnerId && EF.Functions.ILike(f.OwnerType, ownerPattern, "\\")
                && f.Visibility == Visibility.Private && f.Status == FileAssetStatus.Available
                && tenantIds.Contains(EF.Property<string>(f, "TenantId")));
        long total = await files.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await files.OrderByDescending(f => f.CreatedAtUtc).ThenByDescending(f => f.Id)
            .Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResponse<FileAssetDto>
        {
            Items = rows.Select(f => FileAssetMapper.ToDto(f)).ToList(),
            PageNumber = q.PageNumber, PageSize = q.PageSize, TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)q.PageSize),
        };
    }
}
