using FSH.Modules.Chat.Contracts.Authorization;
using FSH.Modules.Chat.Contracts.v1.DTOs;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Chat;

public sealed partial class ChatChannelsTests
{
    [Fact]
    public async Task ChannelManagement_Should_Enforce_Permissions_And_Member_Roles()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        using var member = await OperatorTestUsers.CreateOperatorAsync(_factory,
            ChatPermissions.Channels.View, ChatPermissions.Channels.Create);
        using var candidate = await OperatorTestUsers.CreateOperatorAsync(_factory,
            ChatPermissions.Channels.View);
        using var viewOnly = await OperatorTestUsers.CreateOperatorAsync(_factory,
            ChatPermissions.Channels.View);
        var memberId = await GetCurrentUserIdAsync(member);
        var candidateId = await GetCurrentUserIdAsync(candidate);
        var privateChannel = await CreateChannelAsync(admin, UniqueName("PrivateManage"), isPrivate: true);
        var publicChannel = await CreateChannelAsync(admin, UniqueName("PublicManage"), isPrivate: false);
        await AddMemberAsync(admin, privateChannel, memberId);
        await AddMemberAsync(admin, publicChannel, memberId);

        using var createDenied = await viewOnly.PostAsJsonAsync($"{ChatBasePath}/channels", new
        {
            name = UniqueName("Denied"), description = (string?)null, isPrivate = false,
        });
        createDenied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var privateInvite = await member.PostAsJsonAsync($"{ChatBasePath}/channels/{privateChannel}/members",
            new { channelId = privateChannel, userIds = new[] { candidateId } });
        privateInvite.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var publicInvite = await member.PostAsJsonAsync($"{ChatBasePath}/channels/{publicChannel}/members",
            new { channelId = publicChannel, userIds = new[] { candidateId } });
        publicInvite.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var renameDenied = await member.PutAsJsonAsync($"{ChatBasePath}/channels/{privateChannel}", new
        {
            channelId = privateChannel, name = UniqueName("Intrusion"), description = (string?)null, isPrivate = true,
        });
        using var archiveDenied = await member.DeleteAsync($"{ChatBasePath}/channels/{privateChannel}");
        using var removeOtherDenied = await member.DeleteAsync(
            $"{ChatBasePath}/channels/{publicChannel}/members/{candidateId}");
        renameDenied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        archiveDenied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        removeOtherDenied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var selfLeave = await member.DeleteAsync($"{ChatBasePath}/channels/{privateChannel}/members/{memberId}");
        selfLeave.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var detail = await admin.GetAsync($"{ChatBasePath}/channels/{privateChannel}");
        (await detail.DeserializeAsync<ChannelDto>()).Members.ShouldNotContain(row => row.UserId == memberId);
    }

    private static async Task AddMemberAsync(HttpClient client, Guid channelId, string userId)
    {
        using var response = await client.PostAsJsonAsync($"{ChatBasePath}/channels/{channelId}/members",
            new { channelId, userIds = new[] { userId } });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
