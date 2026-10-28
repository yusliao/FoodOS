using FSH.Framework.Core.Domain;

namespace FSH.Modules.Catalog.Domain;

/// <summary>
/// Which pricing level a customer organization belongs to. At most one row per customer;
/// customers without a row pay the normal catalog price.
/// </summary>
public sealed class CustomerLevelAssignment : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public Guid CustomerOrgId { get; private set; }
    public Guid CustomerLevelId { get; private set; }

    private CustomerLevelAssignment() { }

    public static CustomerLevelAssignment Create(Guid customerOrgId, Guid customerLevelId)
    {
        if (customerOrgId == Guid.Empty)
        {
            throw new ArgumentException("CustomerOrgId is required.", nameof(customerOrgId));
        }

        var assignment = new CustomerLevelAssignment { Id = Guid.CreateVersion7(), CustomerOrgId = customerOrgId };
        assignment.MoveTo(customerLevelId);
        return assignment;
    }

    public void MoveTo(Guid customerLevelId)
    {
        if (customerLevelId == Guid.Empty)
        {
            throw new ArgumentException("CustomerLevelId is required.", nameof(customerLevelId));
        }

        CustomerLevelId = customerLevelId;
    }
}
