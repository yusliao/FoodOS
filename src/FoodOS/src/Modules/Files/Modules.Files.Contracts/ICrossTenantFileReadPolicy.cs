namespace FSH.Modules.Files.Contracts;

/// <summary>
/// Explicit, read-only exception for a shared business resource in the same database.
/// The owning module must validate both the caller's resource access and the file's source tenant.
/// This does not grant file listing, upload finalization, mutation or deletion across tenants.
/// </summary>
public interface ICrossTenantFileReadPolicy : IFileAccessPolicy
{
    Task<bool> CanReadAcrossTenantsAsync(
        FileAccessContext context, string fileTenantId, string currentUserId, CancellationToken cancellationToken);
}
