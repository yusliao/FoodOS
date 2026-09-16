using FSH.Framework.Core.Context;
using FSH.Modules.Files.Contracts;
using FSH.Modules.Files.Contracts.v1.DTOs;
using FSH.Modules.Files.Data;
using FSH.Modules.Files.Domain;
using FSH.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Files.Features.v1.Internal;

internal static class FileReadLookup
{
    public static async Task<FileAsset?> FindAsync(
        FilesDbContext db, FileAccessPolicyRegistry policies, ICurrentUser currentUser,
        Guid fileId, CancellationToken cancellationToken)
    {
        var local = await db.FileAssets.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId, cancellationToken).ConfigureAwait(false);
        if (local is not null) return local;
        if (!currentUser.IsAuthenticated() || currentUser.GetUserId() == Guid.Empty
            || string.IsNullOrWhiteSpace(currentUser.GetTenant())) return null;

        // Exact-ID lookup only: preserve soft deletion and require a finalized private attachment.
        // Cross-tenant reads are denied unless the owning module explicitly validates the resource.
        var candidate = await db.FileAssets.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.Id == fileId && !f.IsDeleted && f.OwnerId != null
                && f.Visibility == Visibility.Private && f.Status == FileAssetStatus.Available)
            .Select(f => new { File = f, TenantId = EF.Property<string>(f, "TenantId") })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (candidate is null || policies.Resolve(candidate.File.OwnerType) is not ICrossTenantFileReadPolicy policy)
            return null;

        var file = candidate.File;
        var context = new FileAccessContext(file.Id, file.OwnerType, file.OwnerId, file.CreatedByUserId, (int)file.Visibility);
        return await policy.CanReadAcrossTenantsAsync(context, candidate.TenantId,
            currentUser.GetUserId().ToString(), cancellationToken).ConfigureAwait(false) ? file : null;
    }
}
