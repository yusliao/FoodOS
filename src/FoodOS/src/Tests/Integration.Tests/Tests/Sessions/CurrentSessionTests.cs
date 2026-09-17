using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Contracts.v1.Tokens.RefreshToken;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Sessions;

[Collection(FshCollectionDefinition.Name)]
public sealed class CurrentSessionTests(FshWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevokeOthers_Should_PreserveSignedCurrentSession_AfterLoginOrRefresh(bool rotate)
    {
        var auth = new AuthHelper(factory);
        using var admin = await auth.CreateRootAdminClientAsync();
        var user = await IdentityUserSeeder.CreateLoginableUserAsync(factory, admin, "current-session");
        var first = await auth.GetTokenAsync(user.Email, user.Password);
        var second = await auth.GetTokenAsync(user.Email, user.Password);
        var currentId = ReadSessionId(second.AccessToken);
        currentId.ShouldNotBe(ReadSessionId(first.AccessToken));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", "root");
        var access = second.AccessToken;
        var refresh = second.RefreshToken;
        if (rotate)
        {
            // A different access token for the same user must not choose the session to refresh.
            using var rotated = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/refresh", new { token = first.AccessToken, refreshToken = refresh });
            rotated.EnsureSuccessStatusCode();
            var body = await rotated.Content.ReadFromJsonAsync<RefreshTokenCommandResponse>();
            body.ShouldNotBeNull();
            access = body.Token;
            refresh = body.RefreshToken;
            ReadSessionId(access).ShouldBe(currentId);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var sessions = await client.GetFromJsonAsync<List<UserSessionDto>>($"{TestConstants.IdentityBasePath}/sessions/me");
        sessions.ShouldNotBeNull();
        sessions.Count.ShouldBe(2);
        sessions.Single(s => s.IsCurrentSession).Id.ShouldBe(currentId);

        using var wrongExclusion = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/sessions/revoke-all", new { exceptSessionId = ReadSessionId(first.AccessToken) });
        wrongExclusion.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var unchanged = await client.GetFromJsonAsync<List<UserSessionDto>>($"{TestConstants.IdentityBasePath}/sessions/me");
        unchanged!.Count.ShouldBe(2);

        // Both the explicit frontend request and the documented empty request preserve current.
        using var revoked = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/sessions/revoke-all", rotate ? new { exceptSessionId = (Guid?)currentId } : new { exceptSessionId = (Guid?)null });
        revoked.EnsureSuccessStatusCode();
        var remaining = await client.GetFromJsonAsync<List<UserSessionDto>>($"{TestConstants.IdentityBasePath}/sessions/me");
        remaining.ShouldNotBeNull();
        remaining.Count.ShouldBe(1);
        remaining[0].Id.ShouldBe(currentId);
        remaining[0].IsCurrentSession.ShouldBeTrue();

        using var stillRefreshable = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/refresh", new { token = access, refreshToken = refresh });
        stillRefreshable.EnsureSuccessStatusCode();
        var final = await stillRefreshable.Content.ReadFromJsonAsync<RefreshTokenCommandResponse>();
        ReadSessionId(final!.Token).ShouldBe(currentId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("malformed")]
    [InlineData("other-user")]
    public async Task UnidentifiedSession_Should_RejectBulkRevoke_AndRecoverAfterRefresh(string? claimVariant)
    {
        var auth = new AuthHelper(factory);
        using var admin = await auth.CreateRootAdminClientAsync();
        var user = await IdentityUserSeeder.CreateLoginableUserAsync(factory, admin, "legacy-session");
        await auth.GetTokenAsync(user.Email, user.Password);
        var login = await auth.GetTokenAsync(user.Email, user.Password);
        var currentId = ReadSessionId(login.AccessToken);
        string? claimedId = claimVariant;
        if (claimVariant == "other-user")
        {
            var other = await IdentityUserSeeder.CreateLoginableUserAsync(factory, admin, "other-session");
            var otherLogin = await auth.GetTokenAsync(other.Email, other.Password);
            claimedId = ReadSessionId(otherLogin.AccessToken).ToString();
        }

        // Re-sign with the isolated host's test key: exercise real JWT authentication,
        // not a mocked principal or an unsigned/tampered token rejected before the handler.
        var legacyAccess = WithSessionClaim(login.AccessToken, claimedId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", "root");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", legacyAccess);
        var before = await client.GetFromJsonAsync<List<UserSessionDto>>($"{TestConstants.IdentityBasePath}/sessions/me");
        before.ShouldNotBeNull();
        before.Count.ShouldBe(2);
        before.ShouldAllBe(session => !session.IsCurrentSession);

        foreach (Guid? excluded in new Guid?[] { null, currentId })
        {
            using var denied = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/sessions/revoke-all", new { exceptSessionId = excluded });
            denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        var unchanged = await client.GetFromJsonAsync<List<UserSessionDto>>($"{TestConstants.IdentityBasePath}/sessions/me");
        unchanged!.Select(session => session.Id).Order().ShouldBe(before.Select(session => session.Id).Order());

        using var refreshed = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/refresh",
            new { token = legacyAccess, refreshToken = login.RefreshToken });
        refreshed.EnsureSuccessStatusCode();
        var rotated = await refreshed.Content.ReadFromJsonAsync<RefreshTokenCommandResponse>();
        rotated.ShouldNotBeNull();
        ReadSessionId(rotated.Token).ShouldBe(currentId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rotated.Token);
        var identified = await client.GetFromJsonAsync<List<UserSessionDto>>($"{TestConstants.IdentityBasePath}/sessions/me");
        identified!.Single(session => session.IsCurrentSession).Id.ShouldBe(currentId);
        using var revokeOthers = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/sessions/revoke-all", new { });
        revokeOthers.EnsureSuccessStatusCode();
        var remaining = await client.GetFromJsonAsync<List<UserSessionDto>>($"{TestConstants.IdentityBasePath}/sessions/me");
        remaining!.Count.ShouldBe(1);
        remaining[0].Id.ShouldBe(currentId);
    }

    private static string WithSessionClaim(string accessToken, string? sessionId)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(accessToken);
        var claims = jwt.Claims.Where(claim => claim.Type is not ("session_id" or "exp" or "nbf" or "iat" or "iss" or "aud")).ToList();
        if (sessionId is not null) claims.Add(new Claim("session_id", sessionId));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSigningKey)), SecurityAlgorithms.HmacSha256);
        return handler.WriteToken(new JwtSecurityToken(TestConstants.JwtIssuer, TestConstants.JwtAudience, claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), credentials));
    }

    private static Guid ReadSessionId(string token) => Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(c => c.Type == "session_id").Value);
}
