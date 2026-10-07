using Integration.Tests.Infrastructure;
using Integration.Tests.Tests.Sessions;
using FSH.Modules.Identity.Contracts.DTOs;
using RoleDto = FSH.Modules.Identity.Contracts.DTOs.RoleDto;
using UserDto = FSH.Modules.Identity.Contracts.DTOs.UserDto;

namespace Integration.Tests.Tests.Authorization;

[Collection(FshCollectionDefinition.Name)]
public sealed class PermissionManagementRegressionTests(FshWebApplicationFactory factory)
{
    private const string Base = TestConstants.IdentityBasePath;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingAccessToken_Should_BeRejected_AfterAccountOrSessionRevocation(bool deactivate)
    {
        var auth = new AuthHelper(factory);
        using var admin = await auth.CreateRootAdminClientAsync();
        var user = await IdentityUserSeeder.CreateLoginableUserAsync(factory, admin, "access-revoke");
        using var client = await auth.CreateAuthenticatedClientAsync(user.Email, user.Password);
        using var otherSession = await auth.CreateAuthenticatedClientAsync(user.Email, user.Password);
        using var before = await client.GetAsync($"{Base}/roles");
        before.StatusCode.ShouldBe(HttpStatusCode.OK);

        if (deactivate)
        {
            using var change = await admin.PatchAsJsonAsync($"{Base}/users/{user.UserId}",
                new { userId = user.UserId, activateUser = false });
            change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        else
        {
            var sessions = await admin.GetFromJsonAsync<List<UserSessionDto>>($"{Base}/users/{user.UserId}/sessions");
            var sessionId = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                .ReadJwtToken(client.DefaultRequestHeaders.Authorization!.Parameter)
                .Claims.Single(claim => claim.Type == "session_id").Value;
            sessions!.Count.ShouldBe(2);
            using var change = await admin.DeleteAsync($"{Base}/users/{user.UserId}/sessions/{sessionId}");
            change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var after = await client.GetAsync($"{Base}/roles");
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // Endpoints requiring authentication alone must also reject the old token.
        using var profile = await client.GetAsync($"{Base}/profile");
        profile.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var otherProfile = await otherSession.GetAsync($"{Base}/profile");
        otherProfile.StatusCode.ShouldBe(deactivate ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RoleWrite_Should_ReportDuplicateName_AndPreserveStoredRole(bool update)
    {
        using var admin = await new AuthHelper(factory).CreateRootAdminClientAsync();
        var name = $"duplicate-{Guid.NewGuid():N}";
        using var first = await admin.PostAsJsonAsync($"{Base}/roles", new { id = "", name, description = "original" });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var original = await first.Content.ReadFromJsonAsync<RoleDto>();
        var id = "";
        if (update)
        {
            using var other = await admin.PostAsJsonAsync($"{Base}/roles", new { id = "", name = name + "-other", description = "other" });
            other.EnsureSuccessStatusCode();
            id = (await other.Content.ReadFromJsonAsync<RoleDto>())!.Id;
        }

        using var duplicate = await admin.PostAsJsonAsync($"{Base}/roles", new { id, name, description = "duplicate" });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var preserved = await admin.GetFromJsonAsync<RoleDto>($"{Base}/roles/{original!.Id}");
        preserved!.Description.ShouldBe("original");
        if (update)
        {
            var other = await admin.GetFromJsonAsync<RoleDto>($"{Base}/roles/{id}");
            other!.Name.ShouldBe(name + "-other");
        }
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("aDmIn")]
    public async Task SelfDemotion_Should_BeRejected_RegardlessOfRoleNameCasing(string roleName)
    {
        var auth = new AuthHelper(factory);
        using var admin = await auth.CreateRootAdminClientAsync();
        var user = await IdentityUserSeeder.CreateLoginableUserAsync(factory, admin, "self-demote");
        using var grant = await admin.PostAsJsonAsync($"{Base}/users/{user.UserId}/roles",
            new { userId = user.UserId, userRoles = new[] { new { roleName = "Admin", enabled = true } } });
        grant.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var client = await auth.CreateAuthenticatedClientAsync(user.Email, user.Password);

        using var demote = await client.PostAsJsonAsync($"{Base}/users/{user.UserId}/roles",
            new { userId = user.UserId, userRoles = new[] { new { roleName, enabled = false } } });
        demote.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var roles = await admin.GetFromJsonAsync<List<UserRoleDto>>($"{Base}/users/{user.UserId}/roles");
        roles!.Single(role => role.RoleName == "Admin").Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task RootRecoveryAdmin_Should_RejectDemotion_WithLowercaseRoleName()
    {
        var auth = new AuthHelper(factory);
        using var admin = await auth.CreateRootAdminClientAsync();
        var root = await admin.GetFromJsonAsync<UserDto>($"{Base}/profile");
        var user = await IdentityUserSeeder.CreateLoginableUserAsync(factory, admin, "root-demote");
        using var grant = await admin.PostAsJsonAsync($"{Base}/users/{user.UserId}/roles",
            new { userId = user.UserId, userRoles = new[] { new { roleName = "Admin", enabled = true } } });
        grant.EnsureSuccessStatusCode();
        using var otherAdmin = await auth.CreateAuthenticatedClientAsync(user.Email, user.Password);
        using var demote = await otherAdmin.PostAsJsonAsync($"{Base}/users/{root!.Id}/roles",
            new { userId = root.Id, userRoles = new[] { new { roleName = "admin", enabled = false } } });
        demote.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var roles = await admin.GetFromJsonAsync<List<UserRoleDto>>($"{Base}/users/{root.Id}/roles");
        roles!.Single(role => role.RoleName == "Admin").Enabled.ShouldBeTrue();
    }
}
