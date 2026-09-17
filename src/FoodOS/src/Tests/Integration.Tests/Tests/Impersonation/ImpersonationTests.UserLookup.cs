using System.Net;
using System.Net.Http.Json;
using FSH.Framework.Shared.Persistence;
using UserDto = FSH.Modules.Identity.Contracts.DTOs.UserDto;
using FSH.Modules.Identity.Contracts.Authorization;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Impersonation;

public sealed partial class ImpersonationTests
{
    [Fact]
    public async Task SupportLookup_Should_StayInExplicitTarget_WithoutChangingRootScope()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        var page = await root.GetFromJsonAsync<PagedResponse<UserDto>>(
            $"{ImpersonationBasePath}/users?targetTenantId={_tenantId}", Json);
        page.ShouldNotBeNull();
        page.Items.ShouldContain(u => u.Id == _tenantAdminUserId);
        page.Items.ShouldNotContain(u => u.Id == _rootAdminUserId);
        var ordinary = await root.GetFromJsonAsync<PagedResponse<UserDto>>(
            $"{TestConstants.IdentityBasePath}/users/search", Json);
        ordinary!.Items.ShouldNotContain(u => u.Id == _tenantAdminUserId);
        using var missing = await root.GetAsync($"{ImpersonationBasePath}/users?targetTenantId=missing-{Guid.NewGuid():N}");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var invalid = await root.GetAsync($"{ImpersonationBasePath}/users?targetTenantId={_tenantId}&pageSize=51");
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SupportLookup_Should_RejectCustomer_AndEmployeeWithoutSupportPermission()
    {
        var token = await GetTokenWithRetryAsync(_tenantAdminEmail, TestConstants.DefaultPassword, _tenantId);
        using var customer = ClientWithBearer(token.AccessToken, _tenantId);
        using var denied = await customer.GetAsync($"{ImpersonationBasePath}/users?targetTenantId=root");
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var employee = await OperatorTestUsers.CreateOperatorAsync(_factory, IdentityPermissions.Users.Search);
        using var employeeDenied = await employee.GetAsync($"{ImpersonationBasePath}/users?targetTenantId={_tenantId}");
        employeeDenied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SupportLookup_Should_RejectInactiveTarget()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using var deactivate = await root.PostAsJsonAsync(
            $"{TestConstants.TenantsBasePath}/{_tenantId}/activation", new { tenantId = _tenantId, isActive = false });
        deactivate.EnsureSuccessStatusCode();
        using var denied = await root.GetAsync($"{ImpersonationBasePath}/users?targetTenantId={_tenantId}");
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
