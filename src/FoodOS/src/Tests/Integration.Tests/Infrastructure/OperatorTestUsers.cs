using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Domain;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Integration.Tests.Infrastructure;

internal static class OperatorTestUsers
{
    public static async Task<HttpClient> CreateOperatorAsync(FshWebApplicationFactory factory, params string[] permissions)
    {
        var auth = new AuthHelper(factory);
        using var scope = factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var name = $"operator-{Guid.NewGuid():N}";
        var user = new FshUser
        {
            UserName = name, Email = $"{name}@example.com", FirstName = "Operator", LastName = "Probe",
            EmailConfirmed = true, IsActive = true,
        };
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>()
            .CreateAsync(user, TestConstants.DefaultPassword);
        result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Description)));
        if (permissions.Length > 0)
        {
            using var admin = await auth.CreateRootAdminClientAsync();
            using var createRole = await admin.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/roles",
                new { id = "", name, description = "Operator permission test" });
            createRole.EnsureSuccessStatusCode();
            var role = await createRole.DeserializeAsync<RoleDto>();
            using var grant = await admin.PutAsJsonAsync($"{TestConstants.IdentityBasePath}/{role.Id}/permissions",
                new { roleId = role.Id, permissions });
            grant.EnsureSuccessStatusCode();
            using var assignRole = await admin.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/users/{user.Id}/roles",
                new { userId = user.Id, userRoles = new[] { new { roleName = role.Name, enabled = true } } });
            assignRole.EnsureSuccessStatusCode();
        }
        return await auth.CreateAuthenticatedClientAsync(user.Email, TestConstants.DefaultPassword);
    }
}
