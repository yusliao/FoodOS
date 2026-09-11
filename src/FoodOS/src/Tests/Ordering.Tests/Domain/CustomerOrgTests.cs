using FSH.Modules.Ordering.Domain;

namespace Ordering.Tests.Domain;

public sealed class CustomerOrgTests
{
    [Fact]
    public void Create_Should_NormalizeCode()
    {
        var org = CustomerOrg.Create("  c000001 ", " Harbor Kitchen ");

        org.Code.ShouldBe("C000001");
        org.Name.ShouldBe("Harbor Kitchen");
        org.CreditHold.ShouldBeFalse();
    }
}
