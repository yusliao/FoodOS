using System.IdentityModel.Tokens.Jwt;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Authentication;

[Collection(FshCollectionDefinition.Name)]
public sealed class TokenGenerationTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public TokenGenerationTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task GenerateToken_Should_ReturnAccessAndRefreshToken_When_CredentialsAreValid()
    {
        var token = await _auth.GetRootAdminTokenAsync();

        token.ShouldNotBeNull();
        token.AccessToken.ShouldNotBeNullOrWhiteSpace();
        token.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        token.AccessTokenExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow);
        token.RefreshTokenExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public async Task GenerateToken_Should_Return401_When_PasswordIsIncorrect()
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Content = JsonContent.Create(new
        {
            email = TestConstants.RootAdminEmail,
            password = "WrongPassword123!"
        });

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("root.admin")]
    [InlineData("ROOT.ADMIN")]
    public async Task GenerateToken_Should_AcceptUserName_AsLoginName(string userName)
    {
        // The seeded root admin's user name is "<tenant>.admin"; lookup is case-insensitive.
        using var client = _factory.CreateClient();
        using var response = await IssueAsync(client, userName, TestConstants.DefaultPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("accessToken");
    }

    [Fact]
    public async Task GenerateToken_Should_Return401_When_UserNameExistsButPasswordIsWrong()
    {
        using var client = _factory.CreateClient();
        using var response = await IssueAsync(client, "root.admin", "WrongPassword123!");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpResponseMessage> IssueAsync(HttpClient client, string login, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Content = JsonContent.Create(new { email = login, password });
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task GenerateToken_Should_Return401_When_EmailDoesNotExist()
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Content = JsonContent.Create(new
        {
            email = "nonexistent@example.com",
            password = TestConstants.DefaultPassword
        });

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GenerateToken_Should_Return400_When_EmailIsEmpty()
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Content = JsonContent.Create(new
        {
            email = "",
            password = TestConstants.DefaultPassword
        });

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GenerateToken_Should_IncludeCorrectClaimsInJwt_When_LoginSucceeds()
    {
        var token = await _auth.GetRootAdminTokenAsync();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.AccessToken);

        jwt.Issuer.ShouldBe(TestConstants.JwtIssuer);
        jwt.Audiences.ShouldContain(TestConstants.JwtAudience);

        var emailClaim = jwt.Claims.FirstOrDefault(c =>
            c.Type is "email" or System.Security.Claims.ClaimTypes.Email);
        emailClaim.ShouldNotBeNull();
        emailClaim.Value.ShouldBe(TestConstants.RootAdminEmail, StringCompareShould.IgnoreCase);
    }

    [Fact]
    public async Task GenerateToken_Should_Fail_When_TenantHeaderIsMissing()
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Content = JsonContent.Create(new
        {
            email = TestConstants.RootAdminEmail,
            password = TestConstants.DefaultPassword
        });

        var response = await client.SendAsync(request);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task GenerateToken_Should_Return403_When_RootUsesDashboardApp()
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Headers.Add("X-FSH-App", "dashboard");
        request.Content = JsonContent.Create(new
        {
            email = TestConstants.RootAdminEmail,
            password = TestConstants.DefaultPassword
        });

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
