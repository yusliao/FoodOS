using System.Net;
using System.Net.Http.Json;
using FSH.Framework.Shared.Persistence;
using UserDto = FSH.Modules.Identity.Contracts.DTOs.UserDto;
using FSH.Modules.Identity.Contracts.Authorization;
using Integration.Tests.Infrastructure;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.Stores;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Integration.Tests.Tests.Impersonation;

public sealed partial class ImpersonationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupportLookup_Should_RejectExpiredOrDedicatedDatabaseTarget(bool dedicatedDatabase)
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using var scope = _factory.Services.CreateScope();
        var stores = scope.ServiceProvider.GetServices<IMultiTenantStore<AppTenantInfo>>().ToList();
        var persistentStore = stores.First(s => s.GetType().Name.StartsWith("EFCoreStore", StringComparison.Ordinal));
        var cacheStore = stores.FirstOrDefault(s => s.GetType() == typeof(DistributedCacheStore<AppTenantInfo>));
        var tenant = await persistentStore.GetAsync(_tenantId);
        tenant.ShouldNotBeNull();
        var originalConnection = tenant.ConnectionString;
        var originalValidity = tenant.ValidUpto;
        try
        {
            // Deliberately unusable: the root lookup must reject the target without connecting to it.
            if (dedicatedDatabase) tenant.ConnectionString = "Host=127.0.0.1;Port=1;Database=unsupported;Timeout=1";
            else tenant.ValidUpto = DateTime.UtcNow.AddYears(-1);
            await persistentStore.UpdateAsync(tenant);
            if (cacheStore is not null) await cacheStore.UpdateAsync(tenant);

            using var denied = await root.GetAsync($"{ImpersonationBasePath}/users?targetTenantId={_tenantId}");
            denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        finally
        {
            tenant.ConnectionString = originalConnection;
            tenant.ValidUpto = originalValidity;
            await persistentStore.UpdateAsync(tenant);
            if (cacheStore is not null) await cacheStore.UpdateAsync(tenant);
        }
    }

    [Fact]
    public async Task SupportLookup_Should_ExcludeDisabledUser_AndReturnItAfterReactivation()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync(_tenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var user = await users.FindByIdAsync(_tenantAdminUserId);
        user.ShouldNotBeNull();
        var path = $"{ImpersonationBasePath}/users?targetTenantId={_tenantId}&search={Uri.EscapeDataString(_tenantAdminEmail)}";
        try
        {
            user.IsActive = false;
            (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
            var disabled = await root.GetFromJsonAsync<PagedResponse<UserDto>>(path, Json);
            disabled.ShouldNotBeNull();
            disabled.Items.ShouldBeEmpty();
        }
        finally
        {
            user.IsActive = true;
            (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        }

        var active = await root.GetFromJsonAsync<PagedResponse<UserDto>>(path, Json);
        active.ShouldNotBeNull();
        active.Items.ShouldContain(u => u.Id == _tenantAdminUserId);
        active.Items.ShouldNotContain(u => u.Id == _rootAdminUserId);
    }

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
