using FSH.Modules.Chat.Contracts.v1.DTOs;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Multitenancy.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Chat;

/// <summary>
/// Cross-TENANT isolation tests for the Chat module. The intra-tenant non-member 404 cases
/// already live in ChatChannelsTests / ChatSendMessageTests / SearchMessagesTests; this class
/// proves the stronger guarantee: a fully provisioned admin in tenant B cannot get, list, join,
/// send-to, or search a channel that belongs to tenant A — every cross-tenant attempt returns 404
/// (or an empty result for list/search), NEVER a leak of the channel's existence or content.
///
/// Tenant A is always <c>root</c> (the seeded tenant). Tenant B is created on the fly via the
/// root admin client, then provisioned, then logged into — mirroring WebhookTenantIsolationTests.
/// All assertions go through the HTTP client + tenant header so the Finbuckle tenant query filter
/// (default-ON via BaseDbContext) is the thing under test.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class ChatTenantIsolationTests
{
    private const string ChatBasePath = "/api/v1/chat";
    private readonly AuthHelper _auth;

    public ChatTenantIsolationTests(FshWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    // ─── get by id ───────────────────────────────────────────────────

    [Fact]
    public async Task GetChannelById_Should_Return404_When_Channel_Belongs_To_Different_Tenant()
    {
        #region Arrange
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var unique = Unique();
        var channelName = $"chat-iso-get-{unique}";
        var channelAId = await CreateChannelAsync(rootClient, channelName);

        using var tenantBClient = await CreateProvisionedTenantAdminClientAsync();
        #endregion

        #region Act
        using var response = await tenantBClient.GetAsync($"{ChatBasePath}/channels/{channelAId}");
        #endregion

        #region Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            "Tenant B must not be able to read tenant A's channel — existence must not leak.");

        // The 404 body is a plain ProblemDetails ("Channel not found.") — it must not carry any of
        // tenant A's channel content. (The channel id legitimately appears in the `instance` URL,
        // since that's the path tenant B itself requested; the leak we guard against is content.)
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(channelName);
        #endregion
    }

    // ─── list ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetChannelMessage_Should_Return404_Across_Tenants_In_Both_Directions()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using var customer = await CreateProvisionedTenantAdminClientAsync();
        var rootChannel = await CreateChannelAsync(root, $"root-lookup-{Unique()}");
        var customerChannel = await CreateChannelAsync(customer, $"customer-lookup-{Unique()}");
        var rootMessage = await SendMessageAsync(root, rootChannel, "operator secret content");
        var customerMessage = await SendMessageAsync(customer, customerChannel, "customer secret content");
        using var crossCustomer = await customer.GetAsync($"{ChatBasePath}/channels/{rootChannel}/messages/{rootMessage}");
        using var crossRoot = await root.GetAsync($"{ChatBasePath}/channels/{customerChannel}/messages/{customerMessage}");
        crossCustomer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        crossRoot.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await crossCustomer.Content.ReadAsStringAsync()).ShouldNotContain("operator secret content");
        (await crossRoot.Content.ReadAsStringAsync()).ShouldNotContain("customer secret content");
    }

    [Fact]
    public async Task ListMyChannels_Should_Not_Include_Channels_From_Different_Tenant()
    {
        #region Arrange
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var unique = Unique();
        var channelAId = await CreateChannelAsync(rootClient, $"chat-iso-list-{unique}");

        using var tenantBClient = await CreateProvisionedTenantAdminClientAsync();
        #endregion

        #region Act
        using var response = await tenantBClient.GetAsync($"{ChatBasePath}/channels");
        #endregion

        #region Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var channels = await response.DeserializeAsync<IReadOnlyList<ChannelDto>>();
        channels.ShouldNotContain(c => c.Id == channelAId,
            "Tenant B's channel list must never surface tenant A's channels.");

        // Discover (public channels not yet joined) must also stay tenant-scoped.
        using var discover = await tenantBClient.GetAsync($"{ChatBasePath}/channels/discover");
        discover.StatusCode.ShouldBe(HttpStatusCode.OK);
        var discoverable = await discover.DeserializeAsync<IReadOnlyList<ChannelDto>>();
        discoverable.ShouldNotContain(c => c.Id == channelAId,
            "Tenant A's public channel must not be discoverable from tenant B.");
        #endregion
    }

    // ─── join (add member) ──────────────────────────────────────────

    [Fact]
    public async Task AddChannelMembers_Should_Return404_When_Channel_Belongs_To_Different_Tenant()
    {
        #region Arrange
        // A public channel in tenant A — even a public channel must be invisible across tenants.
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var unique = Unique();
        var channelAId = await CreateChannelAsync(rootClient, $"chat-iso-join-{unique}", isPrivate: false);

        using var tenantBClient = await CreateProvisionedTenantAdminClientAsync();
        var tenantBUserId = await GetCurrentUserIdAsync(tenantBClient);
        #endregion

        #region Act
        // Tenant B's admin tries to join (add itself to) tenant A's channel.
        using var response = await tenantBClient.PostAsJsonAsync(
            $"{ChatBasePath}/channels/{channelAId}/members",
            new { channelId = channelAId, userIds = new[] { tenantBUserId } });
        #endregion

        #region Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            "Joining a cross-tenant channel must 404 (channel resolution is tenant-filtered).");
        #endregion
    }

    // ─── send ────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_Should_Return404_When_Channel_Belongs_To_Different_Tenant()
    {
        #region Arrange
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var unique = Unique();
        var channelAId = await CreateChannelAsync(rootClient, $"chat-iso-send-{unique}");

        using var tenantBClient = await CreateProvisionedTenantAdminClientAsync();
        #endregion

        #region Act
        using var response = await tenantBClient.PostAsJsonAsync(
            $"{ChatBasePath}/channels/{channelAId}/messages",
            new { body = "cross-tenant intrusion", parentMessageId = (Guid?)null, attachments = Array.Empty<object>() });
        #endregion

        #region Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            "Tenant B must not be able to post into tenant A's channel.");

        // And the message must never have landed: tenant A's listing stays clean.
        using var listForA = await rootClient.GetAsync($"{ChatBasePath}/channels/{channelAId}/messages");
        listForA.StatusCode.ShouldBe(HttpStatusCode.OK);
        var messages = await listForA.DeserializeAsync<IReadOnlyList<MessageDto>>();
        messages.ShouldNotContain(m => m.Body != null && m.Body.Contains("cross-tenant intrusion"),
            "The rejected cross-tenant send must not have persisted into tenant A's channel.");
        #endregion
    }

    // ─── search ──────────────────────────────────────────────────────

    [Fact]
    public async Task Search_Should_Not_Surface_Messages_From_Different_Tenant()
    {
        #region Arrange
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var unique = Unique();
        var token = $"isolatedterm{unique}"; // unique enough to avoid false positives in the shared DB
        var channelAId = await CreateChannelAsync(rootClient, $"chat-iso-search-{unique}");
        await SendMessageAsync(rootClient, channelAId, $"a very {token} secret in tenant A");

        using var tenantBClient = await CreateProvisionedTenantAdminClientAsync();
        #endregion

        #region Act
        // Search from tenant B for the exact token written into tenant A.
        using var response = await tenantBClient.GetAsync(
            $"{ChatBasePath}/search?q={Uri.EscapeDataString(token)}");
        #endregion

        #region Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            "Search returns 200 with zero hits cross-tenant — it must not error or leak.");
        var results = await response.DeserializeAsync<IReadOnlyList<MessageDto>>();
        results.ShouldNotContain(m => m.ChannelId == channelAId,
            "Tenant B's search must never surface tenant A's messages.");
        results.ShouldNotContain(m => m.Body != null && m.Body.Contains(token),
            "Tenant A's message content must not leak into tenant B's search results.");

        // Sanity: tenant A itself can still find its own message (the row really exists).
        using var ownSearch = await rootClient.GetAsync(
            $"{ChatBasePath}/search?q={Uri.EscapeDataString(token)}");
        var ownResults = await ownSearch.DeserializeAsync<IReadOnlyList<MessageDto>>();
        ownResults.ShouldContain(m => m.ChannelId == channelAId,
            "Tenant A must still see its own message — proving B's empty result is isolation, not a missing row.");
        #endregion
    }

    [Fact]
    public async Task Participants_Should_Reject_CrossTenantInvites_AndDirectMessages_InBothDirections()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using var customer = await CreateProvisionedTenantAdminClientAsync();
        string rootUserId = await GetCurrentUserIdAsync(root);
        string customerUserId = await GetCurrentUserIdAsync(customer);
        Guid rootChannel = await CreateChannelAsync(root, $"operator-{Unique()}", isPrivate: true);
        Guid customerChannel = await CreateChannelAsync(customer, $"customer-{Unique()}", isPrivate: true);

        await AssertCannotInviteAsync(root, rootChannel, rootUserId, customerUserId);
        await AssertCannotInviteAsync(customer, customerChannel, customerUserId, rootUserId);
    }

    private static async Task AssertCannotInviteAsync(
        HttpClient client, Guid channelId, string ownUserId, string foreignUserId)
    {
        foreach (string targetId in new[] { foreignUserId, Guid.NewGuid().ToString() })
        {
            using var invite = await client.PostAsJsonAsync($"{ChatBasePath}/channels/{channelId}/members",
                new { userIds = new[] { ownUserId, targetId } });
            invite.StatusCode.ShouldBe(HttpStatusCode.NotFound, await invite.Content.ReadAsStringAsync());
            using var dm = await client.PostAsJsonAsync($"{ChatBasePath}/dms", new { userIds = new[] { targetId } });
            dm.StatusCode.ShouldBe(HttpStatusCode.NotFound, await dm.Content.ReadAsStringAsync());
        }

        using var detail = await client.GetAsync($"{ChatBasePath}/channels/{channelId}");
        detail.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await detail.DeserializeAsync<ChannelDto>()).Members.ShouldHaveSingleItem().UserId.ShouldBe(ownUserId);
    }

    [Fact]
    public async Task DeleteReplyRecovery_Should_Reject_CrossTenant_Deletes_In_Both_Directions()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using var customer = await CreateProvisionedTenantAdminClientAsync();
        var rootChannel = await CreateChannelAsync(root, $"root-delete-{Unique()}");
        var customerChannel = await CreateChannelAsync(customer, $"customer-delete-{Unique()}");
        var rootMessage = await SendMessageAsync(root, rootChannel, "operator message");
        var customerMessage = await SendMessageAsync(customer, customerChannel, "customer message");

        using var crossRoot = await root.DeleteAsync($"{ChatBasePath}/messages/{customerMessage}");
        using var crossCustomer = await customer.DeleteAsync($"{ChatBasePath}/messages/{rootMessage}");
        crossRoot.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        crossCustomer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var ownRoot = await root.GetAsync($"{ChatBasePath}/channels/{rootChannel}/messages/{rootMessage}");
        using var ownCustomer = await customer.GetAsync($"{ChatBasePath}/channels/{customerChannel}/messages/{customerMessage}");
        ownRoot.StatusCode.ShouldBe(HttpStatusCode.OK);
        ownCustomer.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ownRoot.DeserializeAsync<MessageDto>()).Body.ShouldBe("operator message");
        (await ownCustomer.DeserializeAsync<MessageDto>()).Body.ShouldBe("customer message");
    }

    [Fact]
    public async Task EditMessageRecovery_Should_Reject_CrossTenant_Edits_In_Both_Directions()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using var customer = await CreateProvisionedTenantAdminClientAsync();
        var rootChannel = await CreateChannelAsync(root, $"root-edit-{Unique()}");
        var customerChannel = await CreateChannelAsync(customer, $"customer-edit-{Unique()}");
        var rootMessage = await SendMessageAsync(root, rootChannel, "operator original");
        var customerMessage = await SendMessageAsync(customer, customerChannel, "customer original");
        using var crossRoot = await root.PutAsJsonAsync($"{ChatBasePath}/messages/{customerMessage}", new { body = "intrusion" });
        using var crossCustomer = await customer.PutAsJsonAsync($"{ChatBasePath}/messages/{rootMessage}", new { body = "intrusion" });
        crossRoot.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        crossCustomer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var ownRoot = await root.GetAsync($"{ChatBasePath}/channels/{rootChannel}/messages/{rootMessage}");
        using var ownCustomer = await customer.GetAsync($"{ChatBasePath}/channels/{customerChannel}/messages/{customerMessage}");
        (await ownRoot.DeserializeAsync<MessageDto>()).Body.ShouldBe("operator original");
        (await ownCustomer.DeserializeAsync<MessageDto>()).Body.ShouldBe("customer original");
    }

    [Fact]
    public async Task ReadMarker_Should_Reject_CrossTenant_Channels_In_Both_Directions()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using var customer = await CreateProvisionedTenantAdminClientAsync();
        var rootChannel = await CreateChannelAsync(root, $"root-read-{Unique()}");
        var customerChannel = await CreateChannelAsync(customer, $"customer-read-{Unique()}");
        var rootMessage = await SendMessageAsync(root, rootChannel, "operator unread");
        var customerMessage = await SendMessageAsync(customer, customerChannel, "customer unread");

        using var crossRoot = await root.PostAsJsonAsync(
            $"{ChatBasePath}/channels/{customerChannel}/read", new { messageId = customerMessage });
        using var crossCustomer = await customer.PostAsJsonAsync(
            $"{ChatBasePath}/channels/{rootChannel}/read", new { messageId = rootMessage });
        crossRoot.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        crossCustomer.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var ownRoot = await root.GetAsync($"{ChatBasePath}/channels/{rootChannel}");
        using var ownCustomer = await customer.GetAsync($"{ChatBasePath}/channels/{customerChannel}");
        var rootDetail = await ownRoot.DeserializeAsync<ChannelDto>();
        var customerDetail = await ownCustomer.DeserializeAsync<ChannelDto>();
        rootDetail.Members.ShouldHaveSingleItem().LastReadMessageId.ShouldBeNull();
        customerDetail.Members.ShouldHaveSingleItem().LastReadMessageId.ShouldBeNull();
        rootDetail.UnreadCount.ShouldBe(1);
        customerDetail.UnreadCount.ShouldBe(1);
    }

    [Fact]
    public async Task RestoreChannel_Should_Reject_CrossTenant_Channels_In_Both_Directions()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        using var customer = await CreateProvisionedTenantAdminClientAsync();
        string rootName = $"root-restore-{Unique()}";
        string customerName = $"customer-restore-{Unique()}";
        var rootChannel = await CreateChannelAsync(root, rootName);
        var customerChannel = await CreateChannelAsync(customer, customerName);
        using var archiveRoot = await root.DeleteAsync($"{ChatBasePath}/channels/{rootChannel}");
        using var archiveCustomer = await customer.DeleteAsync($"{ChatBasePath}/channels/{customerChannel}");
        archiveRoot.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        archiveCustomer.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var crossRoot = await root.PostAsync($"{ChatBasePath}/channels/{customerChannel}/restore", null);
        using var crossCustomer = await customer.PostAsync($"{ChatBasePath}/channels/{rootChannel}/restore", null);
        crossRoot.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        crossCustomer.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var rootCannotListCustomer = await root.GetAsync(
            $"{ChatBasePath}/channels/trash?search={Uri.EscapeDataString(customerName)}");
        using var customerCannotListRoot = await customer.GetAsync(
            $"{ChatBasePath}/channels/trash?search={Uri.EscapeDataString(rootName)}");
        (await rootCannotListCustomer.DeserializeAsync<PagedResponse<ChannelDto>>()).Items.ShouldBeEmpty();
        (await customerCannotListRoot.DeserializeAsync<PagedResponse<ChannelDto>>()).Items.ShouldBeEmpty();

        using var ownRoot = await root.PostAsync($"{ChatBasePath}/channels/{rootChannel}/restore", null);
        using var ownCustomer = await customer.PostAsync($"{ChatBasePath}/channels/{customerChannel}/restore", null);
        ownRoot.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        ownCustomer.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // ─── helpers ─────────────────────────────────────────────────────

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private static async Task<Guid> CreateChannelAsync(HttpClient client, string name, bool isPrivate = false)
    {
        using var response = await client.PostAsJsonAsync($"{ChatBasePath}/channels", new
        {
            name,
            description = (string?)null,
            isPrivate,
        });
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> SendMessageAsync(HttpClient client, Guid channelId, string body)
    {
        using var response = await client.PostAsJsonAsync(
            $"{ChatBasePath}/channels/{channelId}/messages",
            new { body, parentMessageId = (Guid?)null, attachments = Array.Empty<object>() });
        var dto = await response.DeserializeAsync<MessageDto>();
        return dto.Id;
    }

    private static async Task<string> GetCurrentUserIdAsync(HttpClient client)
    {
        using var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        var user = await response.DeserializeAsync<UserDto>();
        return user.Id;
    }

    /// <summary>
    /// Stands up a brand-new tenant B (via the root admin), waits for its async provisioning to
    /// complete, then returns an authenticated admin client scoped to that tenant. Same flow as
    /// WebhookTenantIsolationTests.
    /// </summary>
    private async Task<HttpClient> CreateProvisionedTenantAdminClientAsync()
    {
        var tenantId = $"chatiso-{Unique()}";
        var adminEmail = $"chatiso-admin-{Unique()}@tenant.com";

        using var rootClient = await _auth.CreateRootAdminClientAsync();
        await CreateTenantAsync(rootClient, tenantId, adminEmail);
        await WaitForProvisioningAsync(rootClient, tenantId);
        return await CreateTenantAdminClientWithRetryAsync(adminEmail, TestConstants.DefaultPassword, tenantId);
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
                var status = (await statusResponse.DeserializeAsync<TenantProvisioningStatusDto>()).Status;
                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
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
