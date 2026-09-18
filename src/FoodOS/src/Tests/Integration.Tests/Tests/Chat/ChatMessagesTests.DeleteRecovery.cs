using FSH.Modules.Chat.Contracts.v1.DTOs;
using FSH.Modules.Chat.Contracts.Authorization;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Chat;

public sealed partial class ChatMessagesTests
{
    [Fact]
    public async Task DeleteReplyRecovery_Should_Decrement_Only_Once_And_Preserve_Other_Replies()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(client, UniqueName("DeleteRetry"));
        var parentId = await SendMessageAsync(client, channelId, "parent");
        var first = await SendMessageAsync(client, channelId, "first reply", parentId);
        var second = await SendMessageAsync(client, channelId, "second reply", parentId);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            using var deletion = await client.DeleteAsync($"{ChatBasePath}/messages/{first}");
            deletion.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            await AssertReplyCountAsync(client, channelId, parentId, 1);
        }
        using var replies = await client.GetAsync($"{ChatBasePath}/messages/{parentId}/replies");
        var rows = await replies.DeserializeAsync<IReadOnlyList<MessageDto>>();
        rows.Single(row => row.Id == first).DeletedAtUtc.ShouldNotBeNull();
        rows.Single(row => row.Id == second).DeletedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteReplyRecovery_Should_Preserve_Count_During_Concurrent_Sends_And_Repeated_Deletes()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(client, UniqueName("Concurrent"));
        var parentId = await SendMessageAsync(client, channelId, "parent");
        var first = await SendMessageAsync(client, channelId, "delete first", parentId);
        var second = await SendMessageAsync(client, channelId, "delete second", parentId);
        await SendMessageAsync(client, channelId, "survivor", parentId);

        await RunConcurrentReplyWritesAsync(client, channelId, parentId, first, second);
        await AssertReplyCountAsync(client, channelId, parentId, 5);
        using var replies = await client.GetAsync($"{ChatBasePath}/messages/{parentId}/replies");
        var rows = await replies.DeserializeAsync<IReadOnlyList<MessageDto>>();
        rows.Count(row => row.DeletedAtUtc is null).ShouldBe(5);
        rows.Count(row => row.DeletedAtUtc is not null).ShouldBe(2);
    }

    [Fact]
    public async Task DeleteReplyRecovery_Should_Require_Membership_Ownership_And_Both_Moderation_Permissions()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(admin, UniqueName("DeleteAccess"));
        var parentId = await SendMessageAsync(admin, channelId, "parent");
        var first = await SendMessageAsync(admin, channelId, "first reply", parentId);
        var second = await SendMessageAsync(admin, channelId, "second reply", parentId);
        using var ownOnly = await OperatorTestUsers.CreateOperatorAsync(_factory,
            ChatPermissions.Channels.View, ChatPermissions.Messages.DeleteOwn);
        using var nonMember = await ownOnly.DeleteAsync($"{ChatBasePath}/messages/{first}");
        nonMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await InviteDeleteTesterAsync(admin, ownOnly, channelId);
        using var denied = await ownOnly.DeleteAsync($"{ChatBasePath}/messages/{first}");
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await AssertReplyCountAsync(admin, channelId, parentId, 2);

        await DeleteReplyAsync(admin, first);
        using var deniedReplay = await ownOnly.DeleteAsync($"{ChatBasePath}/messages/{first}");
        deniedReplay.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await AssertReplyCountAsync(admin, channelId, parentId, 1);

        using var anyOnly = await OperatorTestUsers.CreateOperatorAsync(_factory,
            ChatPermissions.Channels.View, ChatPermissions.Messages.DeleteAny);
        await InviteDeleteTesterAsync(admin, anyOnly, channelId);
        using var endpointDenied = await anyOnly.DeleteAsync($"{ChatBasePath}/messages/{second}");
        endpointDenied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var moderator = await OperatorTestUsers.CreateOperatorAsync(_factory,
            ChatPermissions.Channels.View, ChatPermissions.Messages.DeleteOwn, ChatPermissions.Messages.DeleteAny);
        await InviteDeleteTesterAsync(admin, moderator, channelId);
        await DeleteReplyAsync(moderator, first);
        await AssertReplyCountAsync(admin, channelId, parentId, 1);
        await DeleteReplyAsync(moderator, second);
        await DeleteReplyAsync(moderator, second);
        await AssertReplyCountAsync(admin, channelId, parentId, 0);
    }

    private static async Task InviteDeleteTesterAsync(HttpClient admin, HttpClient tester, Guid channelId)
    {
        using var profile = await tester.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        var userId = (await profile.DeserializeAsync<UserDto>()).Id;
        using var invite = await admin.PostAsJsonAsync($"{ChatBasePath}/channels/{channelId}/members",
            new { userIds = new[] { userId } });
        invite.EnsureSuccessStatusCode();
    }

    private static async Task RunConcurrentReplyWritesAsync(HttpClient client, Guid channelId, Guid parentId, Guid first, Guid second)
    {
        var writes = new List<Task>();
        for (int index = 0; index < 4; index++)
            writes.Add(SendMessageAsync(client, channelId, $"new reply {index}", parentId));
        for (int attempt = 0; attempt < 3; attempt++)
        {
            writes.Add(DeleteReplyAsync(client, first));
            writes.Add(DeleteReplyAsync(client, second));
        }
        await Task.WhenAll(writes);
    }

    private static async Task DeleteReplyAsync(HttpClient client, Guid messageId)
    {
        using var response = await client.DeleteAsync($"{ChatBasePath}/messages/{messageId}");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertReplyCountAsync(HttpClient client, Guid channelId, Guid parentId, int expected)
    {
        using var response = await client.GetAsync($"{ChatBasePath}/channels/{channelId}/messages/{parentId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.DeserializeAsync<MessageDto>()).ReplyCount.ShouldBe(expected);
    }
}
