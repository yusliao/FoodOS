using FSH.Modules.Ordering.Domain;

namespace Ordering.Tests.Domain;

public sealed class StoreContactTests
{
    private static Store CreateStore() =>
        Store.Create(Guid.CreateVersion7(), "s-1", "Harbor", "1 Dock St", Guid.CreateVersion7());

    [Fact]
    public void PhoneNumbers_Should_Keep_Digits_Only()
    {
        PhoneNumbers.Digits("+1 (617) 555-0101").ShouldBe("16175550101");
        PhoneNumbers.Digits(null).ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceContacts_Should_Store_Normalized_Digits_And_Replace_Previous_Contacts()
    {
        var store = CreateStore();
        store.ReplaceContacts([("Front desk", " 617-555-0101 "), ("Chef", "617 555 0111")]);

        store.ReplaceContacts([("Owner", "+86 138 0013 8000")]);

        var contact = store.Contacts.ShouldHaveSingleItem();
        contact.StoreId.ShouldBe(store.Id);
        contact.Name.ShouldBe("Owner");
        contact.Phone.ShouldBe("+86 138 0013 8000");
        contact.PhoneDigits.ShouldBe("8613800138000");
    }

    [Fact]
    public void ReplaceContacts_Should_Reject_The_Same_Number_Twice()
    {
        var store = CreateStore();

        Should.Throw<ArgumentException>(() =>
            store.ReplaceContacts([("A", "617-555-0101"), ("B", "(617) 5550101")]));
        store.Contacts.ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceContacts_Should_Reject_Short_Numbers_And_Too_Many_Contacts()
    {
        var store = CreateStore();

        Should.Throw<ArgumentException>(() => store.ReplaceContacts([("A", "12-3")]));
        Should.Throw<ArgumentException>(() => store.ReplaceContacts(
            Enumerable.Range(0, StoreContact.MaxPerStore + 1)
                .Select(i => ($"C{i}", $"617555{i:D4}"))
                .ToList()));
    }

    [Fact]
    public void PlaceWithPlatformCommitment_Should_Record_Channel_And_Agent()
    {
        var order = SalesOrder.CreateDraft(
            "SO-1",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new DateOnly(2026, 10, 28),
            DateTimeOffset.UtcNow.AddHours(4),
            [(Guid.CreateVersion7(), "Chilled", 2m, 8m, "USD")],
            "acme");
        Guid agent = Guid.CreateVersion7();

        order.PlaceWithPlatformCommitment("key-1", DateTimeOffset.UtcNow, SalesOrderPlacementChannel.OrderDesk, agent);

        order.PlacementChannel.ShouldBe(SalesOrderPlacementChannel.OrderDesk);
        order.PlacedByUserId.ShouldBe(agent);
        order.Status.ShouldBe(SalesOrderStatus.Reserved);
    }
}
