using FSH.Modules.Chat.Contracts.Authorization;
using FSH.Modules.Chat.Contracts.v1.DTOs;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Chat;

public sealed partial class ChatMessagesTests
{
    [Fact]
    public async Task GetChannelMessage_Should_Locate_Parent_And_Reply_Outside_Latest_Page()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(client, UniqueName("Lookup"));
        var parentId = await SendMessageAsync(client, channelId, "older parent");
        var replyId = await SendMessageAsync(client, channelId, "older reply", parentId);
        var latestId = await SendMessageAsync(client, channelId, "latest parent");
        using var latest = await client.GetAsync($"{ChatBasePath}/channels/{channelId}/messages?pageSize=1");
        (await latest.DeserializeAsync<IReadOnlyList<MessageDto>>()).ShouldHaveSingleItem().Id.ShouldBe(latestId);

        foreach (var id in new[] { parentId, replyId })
        {
            using var response = await client.GetAsync($"{ChatBasePath}/channels/{channelId}/messages/{id}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var message = await response.DeserializeAsync<MessageDto>();
            message.Id.ShouldBe(id);
            message.ChannelId.ShouldBe(channelId);
            message.ParentMessageId.ShouldBe(id == replyId ? parentId : null);
            message.Body.ShouldBe(id == replyId ? "older reply" : "older parent");
        }
    }

    [Fact]
    public async Task GetChannelMessage_Should_Reject_Mismatched_Deleted_And_Archived_Targets()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(client, UniqueName("Target"));
        var otherChannelId = await CreateChannelAsync(client, UniqueName("Other"));
        var messageId = await SendMessageAsync(client, channelId, "private message content");
        foreach (var path in new[]
        {
            $"{ChatBasePath}/channels/{otherChannelId}/messages/{messageId}",
            $"{ChatBasePath}/channels/{channelId}/messages/{Guid.NewGuid()}",
        })
        {
            using var response = await client.GetAsync(path);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await response.Content.ReadAsStringAsync()).ShouldNotContain("private message content");
        }

        using var deletion = await client.DeleteAsync($"{ChatBasePath}/messages/{messageId}");
        deletion.EnsureSuccessStatusCode();
        using var deleted = await client.GetAsync($"{ChatBasePath}/channels/{channelId}/messages/{messageId}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var activeMessage = await SendMessageAsync(client, channelId, "archived channel message");
        using var archive = await client.DeleteAsync($"{ChatBasePath}/channels/{channelId}");
        archive.EnsureSuccessStatusCode();
        using var archived = await client.GetAsync($"{ChatBasePath}/channels/{channelId}/messages/{activeMessage}");
        archived.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetChannelMessage_Should_Require_View_And_Membership_Even_For_Public_Channel()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(admin, UniqueName("Member"));
        var messageId = await SendMessageAsync(admin, channelId, "members only content");
        var path = $"{ChatBasePath}/channels/{channelId}/messages/{messageId}";
        using var viewer = await OperatorTestUsers.CreateOperatorAsync(_factory, ChatPermissions.Channels.View);
        using var nonMember = await viewer.GetAsync(path);
        nonMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await nonMember.Content.ReadAsStringAsync()).ShouldNotContain("members only content");

        using var profile = await viewer.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        var userId = (await profile.DeserializeAsync<UserDto>()).Id;
        using var invite = await admin.PostAsJsonAsync($"{ChatBasePath}/channels/{channelId}/members",
            new { userIds = new[] { userId } });
        invite.EnsureSuccessStatusCode();
        using var member = await viewer.GetAsync(path);
        member.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await member.DeserializeAsync<MessageDto>()).Body.ShouldBe("members only content");

        using var removal = await admin.DeleteAsync($"{ChatBasePath}/channels/{channelId}/members/{userId}");
        removal.EnsureSuccessStatusCode();
        using var removedMember = await viewer.GetAsync(path);
        removedMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var noPermission = await OperatorTestUsers.CreateOperatorAsync(_factory);
        using var deniedProfile = await noPermission.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        var deniedUserId = (await deniedProfile.DeserializeAsync<UserDto>()).Id;
        using var deniedInvite = await admin.PostAsJsonAsync($"{ChatBasePath}/channels/{channelId}/members",
            new { userIds = new[] { deniedUserId } });
        deniedInvite.EnsureSuccessStatusCode();
        using var denied = await noPermission.GetAsync(path);
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var anonymous = _factory.CreateClient();
        using var unauthorized = await anonymous.GetAsync(path);
        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetChannelMessage_Should_Validate_Empty_Identifiers()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        foreach (var path in new[]
        {
            $"{ChatBasePath}/channels/{Guid.Empty}/messages/{Guid.NewGuid()}",
            $"{ChatBasePath}/channels/{Guid.NewGuid()}/messages/{Guid.Empty}",
        })
        {
            using var response = await client.GetAsync(path);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }
}
