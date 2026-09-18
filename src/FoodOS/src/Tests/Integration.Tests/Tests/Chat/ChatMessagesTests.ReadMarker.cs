using FSH.Modules.Chat.Contracts.Authorization;
using FSH.Modules.Chat.Contracts.v1.DTOs;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Chat;

public sealed partial class ChatMessagesTests
{
    [Fact]
    public async Task ReadMarker_Should_Ignore_Stale_And_Duplicate_Requests()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(client, UniqueName("ReadOrder"));
        var older = await SendMessageAsync(client, channelId, "older");
        var newer = await SendMessageAsync(client, channelId, "newer");
        await MarkReadAsync(client, channelId, newer);
        await MarkReadAsync(client, channelId, older);
        await MarkReadAsync(client, channelId, newer);
        await AssertReadMarkerAsync(client, channelId, newer);
    }

    [Fact]
    public async Task ReadMarker_Should_Keep_Latest_After_Concurrent_Requests()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(client, UniqueName("ReadRace"));
        var older = await SendMessageAsync(client, channelId, "older");
        var newer = await SendMessageAsync(client, channelId, "newer");
        await RaceReadMarkersAsync(client, channelId, older, newer);
        await AssertReadMarkerAsync(client, channelId, newer);
    }

    [Fact]
    public async Task ReadMarker_Should_Require_View_Membership_And_Message_In_Channel()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(client, UniqueName("ReadScope"));
        var otherChannel = await CreateChannelAsync(client, UniqueName("OtherRead"));
        var id = await SendMessageAsync(client, channelId, "message");
        using var wrongChannel = await client.PostAsJsonAsync($"{ChatBasePath}/channels/{otherChannel}/read", new { messageId = id });
        wrongChannel.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var viewer = await OperatorTestUsers.CreateOperatorAsync(_factory, ChatPermissions.Channels.View);
        using var nonMember = await viewer.PostAsJsonAsync($"{ChatBasePath}/channels/{channelId}/read", new { messageId = id });
        nonMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await InviteDeleteTesterAsync(client, viewer, channelId);
        await MarkReadAsync(viewer, channelId, id);
        using var noView = await OperatorTestUsers.CreateOperatorAsync(_factory);
        await InviteDeleteTesterAsync(client, noView, channelId);
        using var denied = await noView.PostAsJsonAsync($"{ChatBasePath}/channels/{channelId}/read", new { messageId = id });
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task RaceReadMarkersAsync(HttpClient client, Guid channelId, Guid older, Guid newer)
    {
        await Task.WhenAll(MarkReadAsync(client, channelId, older), MarkReadAsync(client, channelId, newer),
            MarkReadAsync(client, channelId, older), MarkReadAsync(client, channelId, newer));
    }

    private static async Task MarkReadAsync(HttpClient client, Guid channelId, Guid messageId)
    {
        using var response = await client.PostAsJsonAsync($"{ChatBasePath}/channels/{channelId}/read", new { messageId });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task AssertReadMarkerAsync(HttpClient client, Guid channelId, Guid expected)
    {
        using var response = await client.GetAsync($"{ChatBasePath}/channels/{channelId}");
        var channel = await response.DeserializeAsync<ChannelDto>();
        channel.Members.ShouldHaveSingleItem().LastReadMessageId.ShouldBe(expected);
        channel.UnreadCount.ShouldBe(0);
    }
}
