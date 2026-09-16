using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using FSH.Modules.Multitenancy.Contracts.Dtos;

namespace Integration.Tests.Tests.Auditing;

/// <summary>
/// Cross-tenant isolation for the by-id / by-correlation / by-trace read
/// endpoints. The existing <c>AuditTenantIsolationTests</c> only covers the
/// paged list endpoint; these cases prove the key-lookup endpoints (which rely
/// on Finbuckle's anonymous tenant query filter rather than an explicit
/// predicate) also refuse to surface another tenant's audit rows.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class AuditQueryTenantIsolationTests
{
    private readonly AuthHelper _auth;

    public AuditQueryTenantIsolationTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task ByIdCorrelationTrace_Should_NotLeakAcrossTenants()
    {
        // Arrange — provision a second tenant and generate audits there.
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var otherTenantId = $"audit-key-iso-{uniqueId}";
        var otherAdminEmail = $"audit-key-admin-{uniqueId}@tenant.com";

        await CreateTenantAsync(rootClient, otherTenantId, otherAdminEmail);
        await WaitForProvisioningAsync(rootClient, otherTenantId);

        using var otherClient = await CreateTenantAdminClientWithRetryAsync(
            otherAdminEmail, TestConstants.DefaultPassword, otherTenantId);

        // Generate an audit row inside the OTHER tenant and capture its keys.
        var otherSeed = await PollForOtherTenantAuditAsync(rootClient, otherTenantId);

        // Restaurant admins cannot use even their own audit keys to access internal audit APIs.
        foreach (var path in new[] { $"/{otherSeed.Id}", $"/by-correlation/{Uri.EscapeDataString(otherSeed.CorrelationId!)}",
            $"/by-trace/{Uri.EscapeDataString(otherSeed.TraceId!)}", "/summary", "/security", "/exceptions" })
        {
            using var response = await otherClient.GetAsync($"{TestConstants.AuditsBasePath}{path}");
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
        }

        // Act + Assert — root tenant must NOT see the other tenant's row.

        // by-id → not-found (Finbuckle filter hides the row; handler raises
        // KeyNotFoundException → 404 in prod, see AuditTestHelper.IsNotFoundAsync).
        var byId = await rootClient.GetAsync($"{TestConstants.AuditsBasePath}/{otherSeed.Id}");
        (await AuditTestHelper.IsNotFoundAsync(byId)).ShouldBeTrue();

        // by-correlation → empty (no rows for that correlation in root's scope).
        var byCorrelation = await AuditTestHelper.GetListAsync(
            rootClient, $"/by-correlation/{Uri.EscapeDataString(otherSeed.CorrelationId!)}");
        byCorrelation.ShouldBeEmpty();

        // by-trace → empty.
        var byTrace = await AuditTestHelper.GetListAsync(
            rootClient, $"/by-trace/{Uri.EscapeDataString(otherSeed.TraceId!)}");
        byTrace.ShouldBeEmpty();
    }

    private static async Task<FSH.Modules.Auditing.Contracts.Dtos.AuditSummaryDto> PollForOtherTenantAuditAsync(
        HttpClient operatorClient, string tenantId)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var page = await AuditTestHelper.GetAuditsPageAsync(operatorClient, extraQuery: $"tenantId={tenantId}");
            var match = page.Items.FirstOrDefault(a => a.TenantId == tenantId
                && !string.IsNullOrEmpty(a.CorrelationId) && !string.IsNullOrEmpty(a.TraceId));
            if (match is not null) return match;
            await Task.Delay(500);
        }
        throw new TimeoutException($"No audit was recorded for {tenantId}.");
    }

    private async Task<HttpClient> CreateTenantAdminClientWithRetryAsync(
        string email, string password, string tenant, int maxRetries = 30)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await _auth.CreateAuthenticatedClientAsync(email, password, tenant);
            }
            catch (HttpRequestException) when (i < maxRetries - 1)
            {
                await Task.Delay(1000);
            }
        }

        return await _auth.CreateAuthenticatedClientAsync(email, password, tenant);
    }

    private static async Task CreateTenantAsync(HttpClient rootClient, string tenantId, string adminEmail)
    {
        var response = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"Tenant {tenantId}",
            connectionString = (string?)null,
            adminEmail,
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer"
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, $"Create tenant failed: {body}");
    }

    private static async Task WaitForProvisioningAsync(HttpClient client, string tenantId, int maxRetries = 60)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var statusResponse = await client.GetAsync(
                $"{TestConstants.TenantsBasePath}/{tenantId}/provisioning");

            if (statusResponse.IsSuccessStatusCode)
            {
                var status = (await statusResponse.DeserializeAsync<TenantProvisioningStatusDto>()).Status;
                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Tenant {tenantId} provisioning failed.");
                }
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException(
            $"Tenant {tenantId} provisioning did not complete within {maxRetries} seconds.");
    }
}
