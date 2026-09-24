using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Domain;
using Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.Tests.Authentication;

/// <summary>
/// Covers TOTP enrollment, verification, enforced login and disable behavior through the
/// real HTTP endpoints. The successful path computes the same standard RFC 6238 code an
/// authenticator app would produce from the shared Base32 key returned by enrollment.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class TwoFactorAuthTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public TwoFactorAuthTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Login_Should_Succeed_WithoutCode_When_TwoFactorIsNotEnabled()
    {
        var (email, password) = await CreateConfirmedUserAsync();

        var response = await LoginAsync(email, password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Enroll_Should_ReturnSharedKey_AndAuthenticatorUri()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        using var client = await SignInAsync(email, password);

        var response = await client.PostAsJsonAsync("/api/v1/identity/2fa/enroll", new { });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var enrollment = await DeserializeAsync<TwoFactorEnrollmentResponse>(response);
        enrollment.ShouldNotBeNull();
        enrollment!.SharedKey.ShouldNotBeNullOrWhiteSpace();
        enrollment.AuthenticatorUri.ShouldStartWith("otpauth://totp/");
        enrollment.AuthenticatorUri.ShouldContain("secret=");
    }

    [Fact]
    public async Task Login_Should_Return401_When_TwoFactorEnabled_And_NoCodeProvided()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        await EnableTwoFactorAsync(email);

        var response = await LoginAsync(email, password);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Should_Return401_When_TwoFactorEnabled_And_WrongCodeProvided()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        await EnableTwoFactorAsync(email);

        var response = await LoginAsync(email, password, "000000");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyEnroll_Should_RejectInvalidCode()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        using var client = await SignInAsync(email, password);
        (await client.PostAsJsonAsync("/api/v1/identity/2fa/enroll", new { })).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/v1/identity/2fa/verify", new { code = "000000" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await IsTwoFactorEnabledAsync(email)).ShouldBeFalse();
    }

    [Fact]
    public async Task Enroll_Login_And_Disable_Should_Succeed_WithValidAuthenticatorCode()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        using var enrollmentClient = await SignInAsync(email, password);

        var enrollmentResponse = await enrollmentClient.PostAsJsonAsync(
            "/api/v1/identity/2fa/enroll",
            new { });
        enrollmentResponse.EnsureSuccessStatusCode();

        var enrollment = await DeserializeAsync<TwoFactorEnrollmentResponse>(enrollmentResponse);
        enrollment.ShouldNotBeNull();
        var code = GenerateAuthenticatorCode(enrollment!.SharedKey, DateTimeOffset.UtcNow);

        var verifyResponse = await enrollmentClient.PostAsJsonAsync(
            "/api/v1/identity/2fa/verify",
            new { code });
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await IsTwoFactorEnabledAsync(email)).ShouldBeTrue();

        using var twoFactorClient = await SignInAsync(email, password, code);
        var disableResponse = await twoFactorClient.PostAsJsonAsync(
            "/api/v1/identity/2fa/disable",
            new { currentPassword = password });
        disableResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await IsTwoFactorEnabledAsync(email)).ShouldBeFalse();

        using var passwordOnlyClient = await SignInAsync(email, password);
    }

    [Fact]
    public async Task Disable_Should_Require_CurrentPassword()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        await EnableTwoFactorAsync(email);

        // Log in WITHOUT 2FA enforcement for simplicity of the negative test — set
        // TwoFactorEnabled=false temporarily so we can acquire a token, then re-enable.
        await SetTwoFactorAsync(email, enabled: false);
        using var client = await SignInAsync(email, password);
        await SetTwoFactorAsync(email, enabled: true);

        var wrongPasswordResponse = await client.PostAsJsonAsync(
            "/api/v1/identity/2fa/disable",
            new { currentPassword = "NotThePassword!1" });
        wrongPasswordResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await IsTwoFactorEnabledAsync(email)).ShouldBeTrue();
    }

    // ---- helpers ----

    private async Task<(string Email, string Password)> CreateConfirmedUserAsync()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var email = $"2fa-{uniqueId}@example.com";
        const string password = "Test@1234!";

        var registerResponse = await rootClient.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/register", new
            {
                firstName = "TwoFactor",
                lastName = "Test",
                email,
                userName = $"twofa-{uniqueId}",
                password,
                confirmPassword = password
            });
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        await WithRootTenantScopeAsync(async userManager =>
        {
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
            (await userManager.ConfirmEmailAsync(user!, token)).Succeeded.ShouldBeTrue();
        });

        return (email, password);
    }

    private async Task<HttpClient> SignInAsync(string email, string password, string? twoFactorCode = null)
    {
        using var loginClient = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Content = JsonContent.Create(new { email, password, twoFactorCode });
        var response = await loginClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var token = await DeserializeAsync<TokenResult>(response);
        token.ShouldNotBeNull();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token!.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);
        return client;
    }

    private async Task<HttpResponseMessage> LoginAsync(string email, string password, string? twoFactorCode = null)
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Content = JsonContent.Create(new { email, password, twoFactorCode });
        return await client.SendAsync(request);
    }

    private async Task EnableTwoFactorAsync(string email)
    {
        await WithRootTenantScopeAsync(async userManager =>
        {
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            await userManager.ResetAuthenticatorKeyAsync(user!);
            await userManager.SetTwoFactorEnabledAsync(user!, true);
        });
    }

    private async Task SetTwoFactorAsync(string email, bool enabled)
    {
        await WithRootTenantScopeAsync(async userManager =>
        {
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            await userManager.SetTwoFactorEnabledAsync(user!, enabled);
        });
    }

    private async Task<bool> IsTwoFactorEnabledAsync(string email)
    {
        bool enabled = false;
        await WithRootTenantScopeAsync(async userManager =>
        {
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            enabled = await userManager.GetTwoFactorEnabledAsync(user!);
        });
        return enabled;
    }

    private async Task WithRootTenantScopeAsync(Func<UserManager<FshUser>, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider
            .GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        await action(userManager);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static string GenerateAuthenticatorCode(string sharedKey, DateTimeOffset timestamp)
    {
        var key = DecodeBase32(sharedKey);
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, timestamp.ToUnixTimeSeconds() / 30);

#pragma warning disable CA5350 // ASP.NET Identity's authenticator provider uses RFC 6238 with HMAC-SHA1.
        var hash = HMACSHA1.HashData(key, counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var binaryCode = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset, sizeof(int)))
            & 0x7fffffff;

        return (binaryCode % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        var output = new byte[normalized.Length * 5 / 8];
        var accumulator = 0;
        var bits = 0;
        var outputIndex = 0;

        foreach (var character in normalized)
        {
            var digit = alphabet.IndexOf(character, StringComparison.Ordinal);
            digit.ShouldBeGreaterThanOrEqualTo(0);
            accumulator = (accumulator << 5) | digit;
            bits += 5;

            if (bits < 8)
            {
                continue;
            }

            bits -= 8;
            output[outputIndex++] = (byte)(accumulator >> bits);
            accumulator &= (1 << bits) - 1;
        }

        return output;
    }
}
