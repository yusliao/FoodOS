using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

public sealed class Store : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public string? CustomerTenantId { get; private set; }
    public Guid CustomerOrgId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Address { get; private set; } = default!;
    public Guid DefaultWarehouseId { get; private set; }
    public Guid? DefaultRouteId { get; private set; }
    public string? DeliveryWindow { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Store() { }

    public static Store Create(
        Guid customerOrgId,
        string code,
        string name,
        string address,
        Guid defaultWarehouseId,
        Guid? defaultRouteId = null,
        string? deliveryWindow = null,
        string? customerTenantId = null)
    {
        if (customerOrgId == Guid.Empty)
        {
            throw new ArgumentException("CustomerOrgId is required.", nameof(customerOrgId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        if (defaultWarehouseId == Guid.Empty)
        {
            throw new ArgumentException("DefaultWarehouseId is required.", nameof(defaultWarehouseId));
        }

        return new Store
        {
            Id = Guid.CreateVersion7(),
            CustomerTenantId = string.IsNullOrWhiteSpace(customerTenantId)
                ? null
                : customerTenantId.Trim().ToUpperInvariant(),
            CustomerOrgId = customerOrgId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Address = address.Trim(),
            DefaultWarehouseId = defaultWarehouseId,
            DefaultRouteId = defaultRouteId,
            DeliveryWindow = string.IsNullOrWhiteSpace(deliveryWindow) ? null : deliveryWindow.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public static Store CreateInitial(
        Guid customerOrgId,
        string code,
        string name,
        string customerTenantId)
    {
        if (customerOrgId == Guid.Empty) throw new ArgumentException("CustomerOrgId is required.", nameof(customerOrgId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerTenantId);

        // The operator's sole warehouse is resolved when ordering, not assigned by the customer.
        return new Store
        {
            Id = Guid.CreateVersion7(),
            CustomerTenantId = customerTenantId.Trim().ToUpperInvariant(),
            CustomerOrgId = customerOrgId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Address = string.Empty,
            DefaultWarehouseId = Guid.Empty,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdateDetails(string name, string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        Name = name.Trim();
        Address = address.Trim();
    }

    public void AssignCustomerTenant(string customerTenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerTenantId);
        CustomerTenantId = customerTenantId.Trim().ToUpperInvariant();
    }
}
