using System.Text;
using System.Text.Json;
using FSH.Modules.Billing.Contracts.Authorization;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Billing;

/// <summary>
/// Coverage for the invoice PDF download endpoint (<c>GET /api/v1/billing/invoices/{id}/pdf</c>):
/// Software subscription invoices are operator-only, not customer goods reconciliation.
/// Root staff need Billing.View; restaurant users cannot download even their former tenant invoice.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class InvoicePdfTests
{
    private const string BillingBasePath = "/api/v1/billing";

    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public InvoicePdfTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
        _factory = factory;
    }

    [Fact]
    public async Task InvoicePdf_Should_Require_OperatorBillingPermission_And_Reject_Customers()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var tenantId = $"pdf-{unique}";
        var adminEmail = $"pdf-{unique}@tenant.com";
        var planKey = await CreatePlanAsync(rootClient, $"pdf-m-{unique}", 29m);
        await CreateTenantAsync(rootClient, tenantId, adminEmail, planKey);
        await WaitForProvisioningAsync(rootClient, tenantId);

        // The subscription invoice was issued on create; grab its id from the operator list.
        var invoiceId = await GetFirstInvoiceIdAsync(rootClient, tenantId);

        using var tenantClient = await CreateTenantAdminClientWithRetryAsync(adminEmail, TestConstants.DefaultPassword, tenantId);

        // Customer ownership does not grant access to the operator's software billing module.
        using var ownResponse = await tenantClient.GetAsync($"{BillingBasePath}/invoices/{invoiceId}/pdf");
        ownResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Root operator → 200: the operator may download ANY tenant's invoice (admin "Download PDF").
        using var rootResponse = await rootClient.GetAsync($"{BillingBasePath}/invoices/{invoiceId}/pdf");
        rootResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
            "the root operator must be able to download any tenant's invoice PDF");

        using var billingReader = await OperatorTestUsers.CreateOperatorAsync(_factory, BillingPermissions.View);
        using var permitted = await billingReader.GetAsync($"{BillingBasePath}/invoices/{invoiceId}/pdf");
        permitted.StatusCode.ShouldBe(HttpStatusCode.OK);
        permitted.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        var bytes = await permitted.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes, 0, 4).ShouldBe("%PDF");
        using var missing = await billingReader.GetAsync($"{BillingBasePath}/invoices/{Guid.NewGuid()}/pdf");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var ordinary = await OperatorTestUsers.CreateOperatorAsync(_factory);
        using var denied = await ordinary.GetAsync($"{BillingBasePath}/invoices/{invoiceId}/pdf");
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);
        using var unauthenticated = await anonymous.GetAsync($"{BillingBasePath}/invoices/{invoiceId}/pdf");
        unauthenticated.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Other customers are denied at the same permission boundary, before reading invoice data.
        var otherUnique = Guid.NewGuid().ToString("N")[..8];
        var otherTenantId = $"pdf-other-{otherUnique}";
        var otherEmail = $"pdf-other-{otherUnique}@tenant.com";
        await CreateTenantAsync(rootClient, otherTenantId, otherEmail, planKey);
        await WaitForProvisioningAsync(rootClient, otherTenantId);
        using var otherClient = await CreateTenantAdminClientWithRetryAsync(
            otherEmail, TestConstants.DefaultPassword, otherTenantId);

        using var crossResponse = await otherClient.GetAsync($"{BillingBasePath}/invoices/{invoiceId}/pdf");
        crossResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden,
            "a tenant must not be able to download another tenant's invoice PDF");
    }

    private static async Task<string> GetFirstInvoiceIdAsync(HttpClient client, string tenantId)
    {
        var resp = await client.GetAsync($"{BillingBasePath}/invoices?tenantId={tenantId}&pageNumber=1&pageSize=50");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        items.GetArrayLength().ShouldBeGreaterThan(0, "the paid-plan tenant must have a subscription invoice");
        return items[0].GetProperty("id").GetString()!;
    }

    private async Task<HttpClient> CreateTenantAdminClientWithRetryAsync(
        string email, string password, string tenant, int maxRetries = 30)
    {
        for (var i = 0; i < maxRetries; i++)
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

    private static async Task<string> CreatePlanAsync(HttpClient client, string key, decimal monthlyBasePrice)
    {
        var resp = await client.PostAsJsonAsync($"{BillingBasePath}/plans",
            new { key, name = $"Plan {key}", currency = "USD", monthlyBasePrice });
        resp.StatusCode.ShouldBe(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return key;
    }

    private static async Task CreateTenantAsync(HttpClient rootClient, string tenantId, string adminEmail, string planKey)
    {
        var response = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"Pdf {tenantId}",
            connectionString = (string?)null,
            adminEmail,
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer",
            planKey,
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, $"Create tenant failed: {body}");
    }

    private static async Task WaitForProvisioningAsync(HttpClient client, string tenantId, int maxRetries = 60)
    {
        for (var i = 0; i < maxRetries; i++)
        {
            var statusResponse = await client.GetAsync($"{TestConstants.TenantsBasePath}/{tenantId}/provisioning");
            if (statusResponse.IsSuccessStatusCode)
            {
                var content = await statusResponse.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(content);
                var status = document.RootElement.GetProperty("status").GetString();
                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Tenant {tenantId} provisioning failed: {content}");
                }
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Tenant {tenantId} did not finish provisioning.");
    }
}
