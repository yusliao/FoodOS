using FSH.Modules.Ordering.Domain;

namespace Ordering.Tests.Domain;

public sealed class CustomerOrgTests
{
    [Fact]
    public void Create_Should_NormalizeCode()
    {
        var org = CustomerOrg.Create("  c000001 ", " Harbor Kitchen ", customerTenantId: " acme ");

        org.Code.ShouldBe("C000001");
        org.Name.ShouldBe("Harbor Kitchen");
        org.CreditHold.ShouldBeFalse();
        org.CustomerTenantId.ShouldBe("ACME");
    }

    [Fact]
    public void AssignCustomerTenant_Should_NormalizeTenantId()
    {
        var org = CustomerOrg.Create("C000002", "Campus Kitchen");

        org.AssignCustomerTenant(" Globex ");

        org.CustomerTenantId.ShouldBe("GLOBEX");
    }
}
