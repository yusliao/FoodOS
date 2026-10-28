using FSH.Framework.Core.Domain;

namespace FSH.Modules.Ordering.Domain;

/// <summary>A person who calls in orders for a store; the phone number identifies the caller.</summary>
public sealed class StoreContact : BaseEntity<Guid>, IOperatorOwnedEntity
{
    public const int MaxPerStore = 10;
    public const int MinPhoneDigits = 5;

    public Guid StoreId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string PhoneDigits { get; private set; } = default!;

    private StoreContact() { }

    internal static StoreContact Create(Guid storeId, string name, string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        string digits = PhoneNumbers.Digits(phone);
        if (digits.Length < MinPhoneDigits)
        {
            throw new ArgumentException($"A contact phone needs at least {MinPhoneDigits} digits.", nameof(phone));
        }

        return new StoreContact
        {
            Id = Guid.CreateVersion7(),
            StoreId = storeId,
            Name = name.Trim(),
            Phone = phone.Trim(),
            PhoneDigits = digits,
        };
    }
}
