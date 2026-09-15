using FSH.Modules.Ordering.Domain;

namespace Ordering.Tests.Domain;

public sealed class CustomerUserStoreAccessTests
{
    [Fact]
    public void Create_Should_NormalizeTenantAndActivateAccess()
    {
        var now = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

        var access = CustomerUserStoreAccess.Create(
            " acme ",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            now);

        access.CustomerTenantId.ShouldBe("ACME");
        access.IsActive.ShouldBeTrue();
        access.CreatedAt.ShouldBe(now);
    }

    [Fact]
    public void SetActive_Should_RevokeAndRestoreAccess()
    {
        var access = CustomerUserStoreAccess.Create(
            "acme",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        access.SetActive(false);
        access.IsActive.ShouldBeFalse();

        access.SetActive(true);
        access.IsActive.ShouldBeTrue();
    }
}
