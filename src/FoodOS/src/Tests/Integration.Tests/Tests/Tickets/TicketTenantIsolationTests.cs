using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Integration.Tests.Tests.Tickets;

/// <summary>
/// Cross-TENANT isolation for the tickets module. Proves a ticket created in
/// tenant A (root) is invisible to tenant B: B cannot fetch it, list it, or
/// mutate it (assign). Reads return 404; operator-only assignment returns 403.
/// The TicketsDbContext gets tenant isolation via BaseDbContext's auto-apply,
/// so these assert intended behavior. Intra-tenant lifecycle / state-machine
/// coverage lives in <see cref="TicketsEndpointTests"/>.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class TicketTenantIsolationTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public TicketTenantIsolationTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task GetTicketById_Should_Return404_When_OwnedByDifferentTenant()
    {
        // Arrange — tenant A (root) creates a ticket; tenant B is freshly provisioned.
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        using var otherClient = await ProvisionTenantClientAsync(rootClient, $"ticket-get-{uniqueId}");

        var ticketId = await CreateTicketAsync(rootClient, $"Ticket-RootOnly-{uniqueId}");

        // Act — tenant B tries to fetch tenant A's ticket.
        using var crossGet = await otherClient.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets/{ticketId}");

        // Assert — clean 404, never tenant A's data.
        crossGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Sanity: tenant A still sees its own ticket.
        using var ownGet = await rootClient.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets/{ticketId}");
        ownGet.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchTickets_Should_NotReturn_OtherTenants_Tickets()
    {
        // Arrange.
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        using var otherClient = await ProvisionTenantClientAsync(rootClient, $"ticket-list-{uniqueId}");

        var rootTitle = $"Ticket-RootOnly-{uniqueId}";
        var ticketId = await CreateTicketAsync(rootClient, rootTitle);

        // Act — tenant B lists tickets.
        using var listResponse = await otherClient.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets?pageNumber=1&pageSize=200");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await listResponse.DeserializeAsync<PagedResult<TicketDto>>();
        var body = await otherClient.GetStringAsync(
            $"{TestConstants.TicketsBasePath}/tickets?pageNumber=1&pageSize=200");

        // Assert — tenant A's ticket never appears in tenant B's listing.
        page.Items.ShouldNotContain(t => t.Id == ticketId,
            "tenant B's ticket list must not include tenant A's ticket");
        body.ShouldNotContain(rootTitle);
    }

    [Fact]
    public async Task AssignTicket_Should_Return403_ForCustomerTenant()
    {
        // Arrange.
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        using var otherClient = await ProvisionTenantClientAsync(rootClient, $"ticket-mut-{uniqueId}");

        var ticketId = await CreateTicketAsync(rootClient, $"Ticket-Mutate-{uniqueId}");

        // Act — tenant B tries to mutate (assign) tenant A's ticket.
        using var crossAssign = await otherClient.PostAsJsonAsync(
            $"{TestConstants.TicketsBasePath}/tickets/{ticketId}/assign",
            new { assigneeUserId = Guid.NewGuid() });

        // Customer tenants cannot use the operator assignment action at all.
        crossAssign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Sanity: tenant A's ticket is untouched — still Open, no assignee.
        using var ownGet = await rootClient.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets/{ticketId}");
        ownGet.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await ownGet.DeserializeAsync<TicketDto>();
        fetched.Status.ShouldBe("Open");
        fetched.AssignedToUserId.ShouldBeNull();
    }

    [Fact]
    public async Task CustomerMember_Should_Not_Read_AnotherMembersTicket_WithinSameTenant()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string tenantId = $"ticket-member-{suffix}";
        string adminEmail = $"{tenantId}-admin@tenant.com";
        await CreateTenantAsync(rootClient, tenantId, adminEmail);
        await WaitForProvisioningAsync(rootClient, tenantId);
        using var tenantAdmin = await CreateTenantAdminClientWithRetryAsync(
            adminEmail,
            TestConstants.DefaultPassword,
            tenantId);

        string memberEmail = $"member-{suffix}@tenant.com";
        Guid memberId = await CreateActiveBasicUserAsync(tenantId, memberEmail, $"member-{suffix}");
        using var member = await CreateTenantAdminClientWithRetryAsync(
            memberEmail,
            TestConstants.DefaultPassword,
            tenantId);

        using var create = await tenantAdmin.PostAsJsonAsync($"{TestConstants.TicketsBasePath}/tickets", new
        {
            title = $"Private participant ticket {suffix}",
            description = "Only the reporter or an assigned participant may see this.",
            priority = "Medium",
            assignedToUserId = memberId,
        });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        Guid ticketId = await create.DeserializeAsync<Guid>();

        using var adminDetail = await tenantAdmin.GetAsync($"{TestConstants.TicketsBasePath}/tickets/{ticketId}");
        var ownTicket = await adminDetail.DeserializeAsync<TicketDto>();
        ownTicket.AssignedToUserId.ShouldBeNull("customer callers cannot appoint an arbitrary assignee");

        using var foreignDetail = await member.GetAsync($"{TestConstants.TicketsBasePath}/tickets/{ticketId}");
        foreignDetail.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var foreignComments = await member.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets/{ticketId}/comments");
        foreignComments.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var foreignList = await member.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets?pageNumber=1&pageSize=200");
        foreignList.StatusCode.ShouldBe(HttpStatusCode.OK, await foreignList.Content.ReadAsStringAsync());
        (await foreignList.DeserializeAsync<PagedResult<TicketDto>>()).Items.ShouldNotContain(
            ticket => ticket.Id == ticketId);
    }

    private async Task<Guid> CreateActiveBasicUserAsync(string tenantId, string email, string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(tenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var user = new FshUser
        {
            FirstName = "Ticket",
            LastName = "Member",
            Email = email,
            UserName = userName,
            EmailConfirmed = true,
            IsActive = true,
        };
        var created = await userManager.CreateAsync(user, TestConstants.DefaultPassword);
        created.Succeeded.ShouldBeTrue(string.Join(", ", created.Errors.Select(error => error.Description)));
        var assigned = await userManager.AddToRoleAsync(user, RoleConstants.Basic);
        assigned.Succeeded.ShouldBeTrue(string.Join(", ", assigned.Errors.Select(error => error.Description)));
        return Guid.Parse(user.Id);
    }

    // ─── helpers ─────────────────────────────────────────────────────

    private static async Task<Guid> CreateTicketAsync(HttpClient client, string title)
    {
        using var response = await client.PostAsJsonAsync($"{TestConstants.TicketsBasePath}/tickets", new
        {
            title,
            description = (string?)null,
            priority = "Medium",
            assignedToUserId = (Guid?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            $"setup failed to create ticket: {await response.Content.ReadAsStringAsync()}");
        return await response.DeserializeAsync<Guid>();
    }

    private async Task<HttpClient> ProvisionTenantClientAsync(HttpClient rootClient, string tenantId)
    {
        var adminEmail = $"{tenantId}-admin@tenant.com";
        await CreateTenantAsync(rootClient, tenantId, adminEmail);
        await WaitForProvisioningAsync(rootClient, tenantId);
        return await CreateTenantAdminClientWithRetryAsync(
            adminEmail, TestConstants.DefaultPassword, tenantId);
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
                var content = await statusResponse.Content.ReadAsStringAsync();
                if (content.Contains("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (content.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Tenant {tenantId} provisioning failed: {content}");
                }
            }

            await Task.Delay(1000);
        }

        var finalResponse = await client.GetAsync(
            $"{TestConstants.TenantsBasePath}/{tenantId}/provisioning");
        var finalContent = finalResponse.IsSuccessStatusCode
            ? await finalResponse.Content.ReadAsStringAsync()
            : $"HTTP {finalResponse.StatusCode}";

        throw new TimeoutException(
            $"Tenant {tenantId} provisioning did not complete within {maxRetries} seconds. Last status: {finalContent}");
    }
}
