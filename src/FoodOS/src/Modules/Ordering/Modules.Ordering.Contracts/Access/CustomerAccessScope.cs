namespace FSH.Modules.Ordering.Contracts.Access;

public sealed record CustomerAccessScope(
    string CustomerTenantId,
    Guid CustomerOrgId,
    IReadOnlyList<Guid> StoreIds);

public interface ICustomerAccessScopeResolver
{
    Task<CustomerAccessScope> ResolveCurrentAsync(CancellationToken cancellationToken = default);

    Task<CustomerAccessScope> ResolveAsync(
        string customerTenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task EnsureCurrentUserCanAccessStoreAsync(
        Guid storeId,
        CancellationToken cancellationToken = default);
}
