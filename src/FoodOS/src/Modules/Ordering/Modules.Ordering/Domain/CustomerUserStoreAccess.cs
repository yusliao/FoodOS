using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

public sealed class CustomerUserStoreAccess : BaseEntity<Guid>, IOperatorOwnedEntity
{
    public string CustomerTenantId { get; private set; } = default!;
    public Guid CustomerOrgId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private CustomerUserStoreAccess() { }

    public static CustomerUserStoreAccess Create(
        string customerTenantId,
        Guid customerOrgId,
        Guid storeId,
        Guid userId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerTenantId);
        if (customerOrgId == Guid.Empty) throw new ArgumentException("CustomerOrgId is required.", nameof(customerOrgId));
        if (storeId == Guid.Empty) throw new ArgumentException("StoreId is required.", nameof(storeId));
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));

        return new CustomerUserStoreAccess
        {
            Id = Guid.CreateVersion7(),
            CustomerTenantId = customerTenantId.Trim().ToUpperInvariant(),
            CustomerOrgId = customerOrgId,
            StoreId = storeId,
            UserId = userId,
            IsActive = true,
            CreatedAt = createdAt,
        };
    }

    public void SetActive(bool active) => IsActive = active;
}
