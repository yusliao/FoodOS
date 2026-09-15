using FSH.Framework.Core.Domain;

namespace FSH.Modules.Logistics.Domain;

public sealed class Driver : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public Guid UserId { get; private set; }
    public string Phone { get; private set; } = default!;

    private Driver() { }

    public static Driver Create(Guid userId, string phone)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        return new Driver
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Phone = phone.Trim()
        };
    }
}
