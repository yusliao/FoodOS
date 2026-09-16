using FSH.Modules.Identity.Contracts.Authorization;
using Integration.Tests.Infrastructure;
using FSH.Framework.Eventing.Abstractions;
using FSH.Modules.Multitenancy.Contracts;

namespace Integration.Tests.Tests.Impersonation;

public sealed partial class ImpersonationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Start_Should_Reject_UnavailableCustomerTenant(bool expired)
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using (_factory.Services.GetRequiredService<IEventTenantScope>().Begin("root"))
        using (var scope = _factory.Services.CreateScope())
        {
            var tenants = scope.ServiceProvider.GetRequiredService<ITenantService>();
            if (expired) await tenants.AdjustValidityAsync(_tenantId, DateTime.UtcNow.AddYears(-1));
            else await tenants.DeactivateAsync(_tenantId);
        }
        using var response = await root.PostAsJsonAsync($"{ImpersonationBasePath}/start",
            new { targetUserId = _tenantAdminUserId, targetTenantId = _tenantId, reason = "unavailable customer" });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ImpersonatedCustomer_Should_NotInheritOperatorPermissions_OrOverrideIdentityDomain()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        var token = await StartImpersonationAsync(root, _tenantAdminUserId, _tenantId);
        using var customer = ClientWithBearer(token, _tenantId);
        using var profile = await customer.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        profile.EnsureSuccessStatusCode();
        (await profile.Content.ReadAsStringAsync()).ShouldContain(_tenantAdminUserId);
        using var permissions = await customer.GetAsync($"{TestConstants.IdentityBasePath}/permissions");
        permissions.EnsureSuccessStatusCode();
        (await permissions.Content.ReadAsStringAsync()).ShouldNotContain(IdentityPermissions.Users.Impersonate);
        foreach (var path in new[] { "/api/v1/logistics/shipments", "/api/v1/identity/impersonation/grants" })
        {
            using var denied = await customer.GetAsync(path);
            denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        using var switchRoot = ClientWithBearer(token, "root");
        using var switched = await switchRoot.GetAsync($"{TestConstants.IdentityBasePath}/users/search");
        switched.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var nested = await customer.PostAsJsonAsync($"{ImpersonationBasePath}/start",
            new { targetUserId = _rootAdminUserId, targetTenantId = "root", reason = "cannot recover actor through start" });
        nested.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var ended = await customer.PostAsJsonAsync($"{ImpersonationBasePath}/end", new { });
        ended.EnsureSuccessStatusCode();
        using var replay = await customer.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OperatorWithoutImpersonatePermission_Should_NotStartCustomerSupportSession()
    {
        using var employee = await OperatorTestUsers.CreateOperatorAsync(_factory, IdentityPermissions.Users.View);
        using var response = await employee.PostAsJsonAsync($"{ImpersonationBasePath}/start",
            new { targetUserId = _tenantAdminUserId, targetTenantId = _tenantId, reason = "not authorized" });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
