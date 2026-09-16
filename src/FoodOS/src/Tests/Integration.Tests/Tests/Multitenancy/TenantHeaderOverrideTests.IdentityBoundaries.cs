using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Constants;
using FSH.Modules.Identity.Contracts.Authorization;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Data;
using FSH.Modules.Identity.Domain;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;
using RoleDto = FSH.Modules.Identity.Contracts.DTOs.RoleDto;
using UserRoleDto = FSH.Modules.Identity.Contracts.DTOs.UserRoleDto;

namespace Integration.Tests.Tests.Multitenancy;

public sealed partial class TenantHeaderOverrideTests
{
    [Fact]
    public async Task UserAndRoleReads_Should_Remain_In_Current_IdentityDomain()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using var a = await _auth.CreateAuthenticatedClientAsync(_tenantAAdminEmail, TestConstants.DefaultPassword, _tenantA);
        using var b = await _auth.CreateAuthenticatedClientAsync(_tenantBAdminEmail, TestConstants.DefaultPassword, _tenantB);
        var clients = new[] { root, a, b };
        var ids = new List<Guid>();
        var roles = new List<RoleDto>();
        foreach (var client in clients)
        {
            ids.Add(await WaveAssignments.UserIdAsync(client));
            using var create = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/roles",
                new { id = "", name = $"Boundary-{Guid.NewGuid():N}", description = "Identity scope regression" });
            create.EnsureSuccessStatusCode();
            roles.Add(await create.DeserializeAsync<RoleDto>());
        }

        for (int i = 0; i < clients.Length; i++)
        {
            using var search = await clients[i].GetAsync($"{TestConstants.IdentityBasePath}/users/search?pageSize=100");
            search.EnsureSuccessStatusCode();
            var page = await search.DeserializeAsync<PagedResult<SearchUserDto>>();
            page.Items.ShouldContain(user => user.Id == ids[i].ToString());
            using var list = await clients[i].GetAsync($"{TestConstants.IdentityBasePath}/users");
            list.EnsureSuccessStatusCode();
            var listText = await list.Content.ReadAsStringAsync();
            foreach (int other in Enumerable.Range(0, clients.Length).Where(index => index != i))
            {
                page.Items.ShouldNotContain(user => user.Id == ids[other].ToString());
                listText.ShouldNotContain(ids[other].ToString());
                foreach (var path in new[] { $"users/{ids[other]}", $"users/{ids[other]}/roles", $"roles/{roles[other].Id}", $"{roles[other].Id}/permissions" })
                {
                    using var detail = await clients[i].GetAsync($"{TestConstants.IdentityBasePath}/{path}");
                    detail.StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
                }
                using var foreignRoleSearch = await clients[i].GetAsync($"{TestConstants.IdentityBasePath}/users/search?roleId={roles[other].Id}");
                foreignRoleSearch.EnsureSuccessStatusCode();
                (await foreignRoleSearch.DeserializeAsync<PagedResult<SearchUserDto>>()).Items.ShouldBeEmpty();
            }
        }

        // Without a header, signed tenant claims still establish the correct query scope.
        a.DefaultRequestHeaders.Remove("tenant");
        using var own = await a.GetAsync($"{TestConstants.IdentityBasePath}/users/search");
        own.EnsureSuccessStatusCode();
        (await own.DeserializeAsync<PagedResult<SearchUserDto>>()).Items.ShouldContain(user => user.Email == _tenantAAdminEmail);
        foreach (var target in new[] { "root", _tenantB })
        {
            using var forged = await a.GetAsync($"{TestConstants.IdentityBasePath}/users/search?tenant={target}");
            forged.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task LegacyWrongAudienceRoles_And_OperatorClaims_Should_NotLeakOrGrantCustomerAccess()
    {
        using var a = await _auth.CreateAuthenticatedClientAsync(_tenantAAdminEmail, TestConstants.DefaultPassword, _tenantA);
        var userId = (await WaveAssignments.UserIdAsync(a)).ToString();
        string wrongRoleId = "";
        string customerRoleId = "";
        var wrongRoleName = $"OperatorLegacy-{Guid.NewGuid():N}";
        using (_factory.Services.GetRequiredService<IEventTenantScope>().Begin(_tenantA))
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<FshRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
            var wrong = new FshRole(wrongRoleName, "Legacy wrong domain", RoleAudiences.Operator);
            var customer = new FshRole($"CustomerLegacy-{Guid.NewGuid():N}", "Legacy overgrant", RoleAudiences.Customer);
            (await roles.CreateAsync(wrong)).Succeeded.ShouldBeTrue();
            (await roles.CreateAsync(customer)).Succeeded.ShouldBeTrue();
            wrongRoleId = wrong.Id;
            customerRoleId = customer.Id;
            var user = (await users.FindByIdAsync(userId)).ShouldNotBeNull();
            foreach (var role in new[] { wrong, customer })
            {
                (await users.AddToRoleAsync(user, role.Name!)).Succeeded.ShouldBeTrue();
                (await roles.AddClaimAsync(role, new Claim(ClaimConstants.Permission, IdentityPermissions.Users.Impersonate))).Succeeded.ShouldBeTrue();
            }
            await scope.ServiceProvider.GetRequiredService<IUserPermissionService>().InvalidatePermissionCacheAsync(userId, CancellationToken.None);
        }
        using var roleList = await a.GetAsync($"{TestConstants.IdentityBasePath}/roles?pageSize=200");
        roleList.EnsureSuccessStatusCode();
        (await roleList.DeserializeAsync<PagedResult<RoleDto>>()).Items.ShouldNotContain(role => role.Id == wrongRoleId);
        using var userRoles = await a.GetAsync($"{TestConstants.IdentityBasePath}/users/{userId}/roles");
        userRoles.EnsureSuccessStatusCode();
        (await userRoles.DeserializeAsync<List<UserRoleDto>>()).ShouldNotContain(role => role.RoleId == wrongRoleId);
        using var roleDetail = await a.GetAsync($"{TestConstants.IdentityBasePath}/{wrongRoleId}/permissions");
        roleDetail.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var permissions = await a.GetAsync($"{TestConstants.IdentityBasePath}/permissions");
        permissions.EnsureSuccessStatusCode();
        (await permissions.Content.ReadAsStringAsync()).ShouldNotContain(IdentityPermissions.Users.Impersonate);
        using var customerClaims = await a.GetAsync($"{TestConstants.IdentityBasePath}/{customerRoleId}/permissions");
        customerClaims.EnsureSuccessStatusCode();
        (await customerClaims.Content.ReadAsStringAsync()).ShouldNotContain(IdentityPermissions.Users.Impersonate);
        using var roleSearch = await a.GetAsync($"{TestConstants.IdentityBasePath}/users/search?roleId={wrongRoleId}");
        roleSearch.EnsureSuccessStatusCode();
        (await roleSearch.DeserializeAsync<PagedResult<SearchUserDto>>()).Items.ShouldBeEmpty();
        using var impersonate = await a.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/impersonation/start",
            new { targetUserId = userId, targetTenantId = _tenantA, reason = "legacy permission must not authorize" });
        impersonate.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var login = await _auth.GetTokenAsync(_tenantAAdminEmail, TestConstants.DefaultPassword, _tenantA);
        new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken).Claims.ShouldNotContain(claim => claim.Value == wrongRoleName);
    }
}
