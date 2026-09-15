using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

public sealed class CustomerOrg : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public string? CustomerTenantId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool CreditHold { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CustomerOrg() { }

    public static CustomerOrg Create(
        string code,
        string name,
        bool creditHold = false,
        string? customerTenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new CustomerOrg
        {
            Id = Guid.CreateVersion7(),
            CustomerTenantId = NormalizeTenantId(customerTenantId),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            CreditHold = creditHold,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void SetCreditHold(bool creditHold) => CreditHold = creditHold;

    public void AssignCustomerTenant(string customerTenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerTenantId);
        CustomerTenantId = NormalizeTenantId(customerTenantId);
    }

    private static string? NormalizeTenantId(string? tenantId)
        => string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim().ToUpperInvariant();
}
