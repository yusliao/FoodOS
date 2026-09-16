using System.Collections.Concurrent;
using System.Text.Json;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Domain;
using FSH.Modules.Notifications.Contracts.Authorization;
using FSH.Modules.Notifications.Contracts.v1.DTOs;
using FSH.Modules.Notifications.IntegrationEventHandlers;
using FSH.Modules.Tickets.Contracts.Authorization;
using FSH.Modules.Tickets.Contracts.Notifications;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR.Client;

namespace Integration.Tests.Tests.Tickets;

public sealed partial class TicketTenantIsolationTests
{
    [Fact]
    public async Task TicketActivities_Should_Notify_CurrentParticipants_AcrossDomains_Without_Leaking_Content()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tenantA = $"notice-a-{suffix}";
        var tenantB = $"notice-b-{suffix}";
        using var a = await ProvisionTenantClientAsync(admin, tenantA);
        using var b = await ProvisionTenantClientAsync(admin, tenantB);
        var memberEmail = $"notice-member-{suffix}@example.com";
        await CreateActiveBasicUserAsync(tenantA, memberEmail, $"notice-member-{suffix}");
        using var member = await _auth.CreateAuthenticatedClientAsync(memberEmail, TestConstants.DefaultPassword, tenantA);
        using var support = await CreateNotificationOperatorAsync(TicketsPermissions.Tickets.View,
            TicketsPermissions.Tickets.Assign, TicketsPermissions.Tickets.Comment, TicketsPermissions.Tickets.Resolve);
        using var replacement = await CreateNotificationOperatorAsync(TicketsPermissions.Tickets.View, TicketsPermissions.Tickets.Comment);
        using var ordinary = await CreateNotificationOperatorAsync();
        var supportId = await WaveAssignments.UserIdAsync(support);
        var replacementId = await WaveAssignments.UserIdAsync(replacement);

        var ticketA = await CreateTicketAsync(a, "private customer A title");
        var ticketB = await CreateTicketAsync(b, "private customer B title");
        (await ReadTicketNotificationsAsync(support, ticketA)).ShouldContain(n => n.Type == "tickets.created");
        (await ReadTicketNotificationsAsync(support, ticketB)).ShouldContain(n => n.Type == "tickets.created");
        (await ReadTicketNotificationsAsync(replacement, ticketA)).ShouldBeEmpty("View alone must not subscribe staff to the queue");
        (await ReadTicketNotificationsAsync(ordinary, ticketA)).ShouldBeEmpty();
        (await ReadTicketNotificationsAsync(a, ticketA)).ShouldBeEmpty("Do not notify the author about their own action");
        await AssignNotificationTicketAsync(admin, ticketA, supportId);

        await using var hubA = await ConnectNotificationHubAsync(a, tenantA);
        await using var hubB = await ConnectNotificationHubAsync(b, tenantB);
        await using var hubMember = await ConnectNotificationHubAsync(member, tenantA);
        var received = new TaskCompletionSource<NotificationDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var forbidden = new ConcurrentQueue<NotificationDto>();
        using var aSubscription = hubA.On<NotificationDto>("NotificationCreated", n =>
        {
            if (n.Type == "tickets.comment") received.TrySetResult(n);
        });
        using var bSubscription = hubB.On<NotificationDto>("NotificationCreated", forbidden.Enqueue);
        using var memberSubscription = hubMember.On<NotificationDto>("NotificationCreated", forbidden.Enqueue);
        await CommentForNotificationAsync(support, ticketA, "private support response must not enter notifications");
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pushed.Link.ShouldBe($"/tickets/{ticketA}");
        pushed.Body.ShouldBeNull();
        await Task.Delay(250);
        forbidden.ShouldBeEmpty();
        (await ReadTicketNotificationsAsync(b, ticketA)).ShouldBeEmpty();
        (await ReadTicketNotificationsAsync(member, ticketA)).ShouldBeEmpty();

        a.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN");
        var reply = (await ReadTicketNotificationsAsync(a, ticketA)).Single(n => n.Type == "tickets.comment");
        reply.Title.ShouldBe("工单有新回复");
        reply.Body.ShouldBeNull();
        using (var metadata = JsonDocument.Parse(reply.MetadataJson))
        {
            metadata.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)
                .ShouldBe(new[] { "activity", "ticketId" });
        }
        using var forbiddenRead = await b.PostAsync($"/api/v1/notifications/{reply.Id}/read", null);
        forbiddenRead.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var peerRead = await member.PostAsync($"/api/v1/notifications/{reply.Id}/read", null);
        peerRead.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadTicketNotificationsAsync(a, ticketA)).Single(n => n.Id == reply.Id).ReadAtUtc.ShouldBeNull();

        await CommentForNotificationAsync(a, ticketA, "customer reply");
        (await ReadTicketNotificationsAsync(support, ticketA)).Count(n => n.Type == "tickets.comment").ShouldBe(1);
        await AssignNotificationTicketAsync(admin, ticketA, replacementId);
        await CommentForNotificationAsync(a, ticketA, "after reassignment");
        (await ReadTicketNotificationsAsync(support, ticketA)).Count(n => n.Type == "tickets.comment").ShouldBe(1);
        (await ReadTicketNotificationsAsync(replacement, ticketA)).Count(n => n.Type == "tickets.comment").ShouldBe(1);

        await SetNotificationUserActiveAsync(replacementId, false);
        await CommentForNotificationAsync(a, ticketA, "inactive assignee must not receive this");
        await SetNotificationUserActiveAsync(replacementId, true);
        (await ReadTicketNotificationsAsync(replacement, ticketA)).Count(n => n.Type == "tickets.comment").ShouldBe(1);

        using var resolved = await support.PostAsJsonAsync($"/api/v1/tickets/{ticketA}/resolve", new { resolutionNote = "private resolution" });
        resolved.EnsureSuccessStatusCode();
        var statusBeforeClose = (await ReadTicketNotificationsAsync(replacement, ticketA)).Count(n => n.Type == "tickets.status-changed");
        using var closed = await a.PostAsync($"/api/v1/tickets/{ticketA}/close", null);
        closed.EnsureSuccessStatusCode();
        (await ReadTicketNotificationsAsync(replacement, ticketA)).Count(n => n.Type == "tickets.status-changed")
            .ShouldBe(statusBeforeClose + 1);
        await CommentForNotificationAsync(support, ticketB, "private customer B response");
        (await ReadTicketNotificationsAsync(b, ticketB)).ShouldContain(n => n.Type == "tickets.comment");
        (await ReadTicketNotificationsAsync(a, ticketB)).ShouldBeEmpty();
        (await ReadTicketNotificationsAsync(b, ticketA)).ShouldBeEmpty();
        forbidden.ShouldNotContain(n => n.Link == $"/tickets/{ticketA}");
    }

    [Fact]
    public async Task TicketActivityDelivery_Should_Deduplicate_Concurrent_Replay_And_Recheck_Deleted_Or_WrongOwner()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        using var support = await CreateNotificationOperatorAsync(TicketsPermissions.Tickets.View);
        var supportId = await WaveAssignments.UserIdAsync(support);
        var ticketId = await CreateTicketAsync(admin, $"replay-{Guid.NewGuid():N}");
        await AssignNotificationTicketAsync(admin, ticketId, supportId);
        var activity = new TicketActivityIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "root", "test",
            "Tickets", ticketId, "root", "root", Guid.Empty, TicketActivityKind.CommentAdded);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => DeliverTicketActivityAsync(activity)));
        (await ReadTicketNotificationsAsync(support, ticketId)).Count(n => n.Type == "tickets.comment").ShouldBe(1);
        await DeliverTicketActivityAsync(activity);
        await DeliverTicketActivityAsync(activity with { Id = Guid.NewGuid(), CustomerTenantId = "wrong-owner" });
        (await ReadTicketNotificationsAsync(support, ticketId)).Count(n => n.Type == "tickets.comment").ShouldBe(1);

        using var withoutTicketView = await CreateNotificationOperatorAsync();
        await AssignNotificationTicketAsync(admin, ticketId, await WaveAssignments.UserIdAsync(withoutTicketView));
        await DeliverTicketActivityAsync(activity with { Id = Guid.NewGuid() });
        (await ReadTicketNotificationsAsync(withoutTicketView, ticketId)).ShouldBeEmpty("Assignment does not bypass Tickets.View");
        await AssignNotificationTicketAsync(admin, ticketId, supportId);

        using var deleted = await admin.DeleteAsync($"/api/v1/tickets/{ticketId}");
        deleted.EnsureSuccessStatusCode();
        await DeliverTicketActivityAsync(activity with { Id = Guid.NewGuid() });
        (await ReadTicketNotificationsAsync(support, ticketId)).Count(n => n.Type == "tickets.comment").ShouldBe(1);
        await Should.ThrowAsync<InvalidOperationException>(() => DeliverTicketActivityAsync(activity with { TenantId = "other" }));
    }

    private async Task DeliverTicketActivityAsync(TicketActivityIntegrationEvent activity)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync("root");
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        await ActivatorUtilities.CreateInstance<TicketActivityNotificationHandler>(scope.ServiceProvider).HandleAsync(activity);
    }

    private async Task SetNotificationUserActiveAsync(Guid userId, bool active)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync("root");
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        user.ShouldNotBeNull();
        user.IsActive = active;
        (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
    }

    private Task<HttpClient> CreateNotificationOperatorAsync(params string[] permissions) =>
        OperatorTestUsers.CreateOperatorAsync(_factory,
            [NotificationPermissions.Inbox.View, NotificationPermissions.Inbox.MarkRead, .. permissions]);

    private async Task<HubConnection> ConnectNotificationHubAsync(HttpClient client, string tenantId)
    {
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var hub = new HubConnectionBuilder().WithUrl(
            $"http://localhost/api/v1/realtime/hub?access_token={Uri.EscapeDataString(token)}", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                options.Headers["tenant"] = tenantId;
            }).Build();
        await hub.StartAsync();
        return hub;
    }

    private static async Task<IReadOnlyList<NotificationDto>> ReadTicketNotificationsAsync(HttpClient client, Guid ticketId)
    {
        using var response = await client.GetAsync("/api/v1/notifications/?pageSize=200");
        response.EnsureSuccessStatusCode();
        var all = await response.DeserializeAsync<IReadOnlyList<NotificationDto>>();
        return all.Where(n => n.Link == $"/tickets/{ticketId}").ToArray();
    }

    private static async Task AssignNotificationTicketAsync(HttpClient client, Guid ticketId, Guid assigneeUserId)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/assign", new { assigneeUserId });
        response.EnsureSuccessStatusCode();
    }

    private static async Task CommentForNotificationAsync(HttpClient client, Guid ticketId, string body)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/comments", new { body });
        response.EnsureSuccessStatusCode();
    }
}
