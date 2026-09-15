using FSH.Framework.Core.Domain;

namespace FSH.Modules.Warehouse.Domain;

public sealed class PackToteOrder : BaseEntity<Guid>, IOperatorOwnedEntity
{
    public Guid PackToteId { get; private set; }
    public Guid OrderId { get; private set; }

    private PackToteOrder() { }

    public static PackToteOrder Create(Guid packToteId, Guid orderId)
        => new()
        {
            Id = Guid.CreateVersion7(),
            PackToteId = packToteId,
            OrderId = orderId
        };
}
