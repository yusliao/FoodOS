using FSH.Modules.Chat.Contracts.Authorization;
using FSH.Modules.Chat.Contracts.v1.DTOs;
using FSH.Framework.Shared.Persistence;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Chat;

public sealed partial class ChatChannelsTests
{
    [Fact]
    public async Task ArchivedChannels_Should_Search_Page_Restore_And_Require_ManageAll()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        string prefix = UniqueName("ArchivedList");
        var ids = new List<Guid>();
        for (int index = 0; index < 3; index++)
        {
            var id = await CreateChannelAsync(admin, $"{prefix}-{index}");
            using var archive = await admin.DeleteAsync($"{ChatBasePath}/channels/{id}");
            archive.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            ids.Add(id);
        }

        using var first = await admin.GetAsync(
            $"{ChatBasePath}/channels/trash?search={Uri.EscapeDataString(prefix)}&pageNumber=1&pageSize=2");
        var page1 = await first.DeserializeAsync<PagedResponse<ChannelDto>>();
        page1.TotalCount.ShouldBe(3);
        page1.TotalPages.ShouldBe(2);
        page1.Items.Count.ShouldBe(2);
        page1.HasNext.ShouldBeTrue();
        page1.Items.ShouldAllBe(channel => channel.Name!.StartsWith(prefix, StringComparison.Ordinal));

        using var second = await admin.GetAsync(
            $"{ChatBasePath}/channels/trash?search={Uri.EscapeDataString(prefix)}&pageNumber=2&pageSize=2");
        var page2 = await second.DeserializeAsync<PagedResponse<ChannelDto>>();
        page2.Items.ShouldHaveSingleItem();
        page2.HasPrevious.ShouldBeTrue();
        page1.Items.Select(channel => channel.Id).Intersect(page2.Items.Select(channel => channel.Id)).ShouldBeEmpty();

        using var restore = await admin.PostAsync($"{ChatBasePath}/channels/{ids[0]}/restore", null);
        restore.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var afterRestore = await admin.GetAsync(
            $"{ChatBasePath}/channels/trash?search={Uri.EscapeDataString(prefix)}&pageNumber=1&pageSize=200");
        var remaining = await afterRestore.DeserializeAsync<PagedResponse<ChannelDto>>();
        remaining.TotalCount.ShouldBe(2);
        remaining.Items.ShouldNotContain(channel => channel.Id == ids[0]);

        using var viewOnly = await OperatorTestUsers.CreateOperatorAsync(_factory, ChatPermissions.Channels.View);
        using var forbidden = await viewOnly.GetAsync($"{ChatBasePath}/channels/trash");
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var invalid = await admin.GetAsync($"{ChatBasePath}/channels/trash?pageNumber=0&pageSize=201");
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

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
