namespace FSH.Modules.Files.Contracts;

/// <summary>
/// Opt-in listing of one authorized owner's finalized private attachments. The owning module
/// resolves the allowed source identity domains; null denies access, including to the count.
/// This does not authorize writes or tenant-wide file browsing.
/// </summary>
public interface IFileOwnerListPolicy : IFileAccessPolicy
{
    Task<IReadOnlyCollection<string>?> GetReadTenantIdsAsync(
        Guid ownerId, string currentUserId, CancellationToken cancellationToken);
}
