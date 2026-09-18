using FSH.Modules.Chat.Contracts.Authorization;
using FSH.Modules.Chat.Contracts.v1.DTOs;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Chat;

public sealed partial class ChatMessagesTests
{
    [Fact]
    public async Task EditMessageRecovery_Should_Return409_After_Deletion_Without_Restoring_Body()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(client, UniqueName("EditDeleted"));
        var id = await SendMessageAsync(client, channelId, "original");
        await DeleteReplyAsync(client, id);
        using var edit = await client.PutAsJsonAsync($"{ChatBasePath}/messages/{id}", new { body = "must not return" });
        edit.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await AssertDeletedBodyAsync(client, channelId, id);
    }

    [Fact]
    public async Task EditMessageRecovery_Should_Require_Membership_And_Author_Even_With_Moderation()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(admin, UniqueName("EditAccess"));
        var id = await SendMessageAsync(admin, channelId, "original");
        using var editor = await OperatorTestUsers.CreateOperatorAsync(_factory,
            ChatPermissions.Channels.View, ChatPermissions.Messages.EditOwn, ChatPermissions.Messages.DeleteAny);
        using var nonMember = await editor.PutAsJsonAsync($"{ChatBasePath}/messages/{id}", new { body = "not a member" });
        nonMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await InviteDeleteTesterAsync(admin, editor, channelId);
        using var nonAuthor = await editor.PutAsJsonAsync($"{ChatBasePath}/messages/{id}", new { body = "not the author" });
        nonAuthor.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var original = await admin.GetAsync($"{ChatBasePath}/channels/{channelId}/messages/{id}");
        (await original.DeserializeAsync<MessageDto>()).Body.ShouldBe("original");
        await DeleteReplyAsync(admin, id);
        using var deletedNonAuthor = await editor.PutAsJsonAsync($"{ChatBasePath}/messages/{id}", new { body = "deleted" });
        deletedNonAuthor.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EditMessageRecovery_Should_Keep_Deleted_Body_Null_When_Edit_And_Delete_Race()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(client, UniqueName("EditRace"));
        for (int iteration = 0; iteration < 5; iteration++)
        {
            var id = await SendMessageAsync(client, channelId, "original");
            await RaceEditAndDeleteAsync(client, id);
            await AssertDeletedBodyAsync(client, channelId, id);
        }
    }

    private static async Task RaceEditAndDeleteAsync(HttpClient client, Guid id)
    {
        var edit = client.PutAsJsonAsync($"{ChatBasePath}/messages/{id}", new { body = "racing edit" });
        var delete = client.DeleteAsync($"{ChatBasePath}/messages/{id}");
        await Task.WhenAll(edit, delete);
        using var editResponse = await edit;
        using var deleteResponse = await delete;
        editResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.NoContent, HttpStatusCode.Conflict);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task AssertDeletedBodyAsync(HttpClient client, Guid channelId, Guid id)
    {
        using var list = await client.GetAsync($"{ChatBasePath}/channels/{channelId}/messages");
        var row = (await list.DeserializeAsync<IReadOnlyList<MessageDto>>()).Single(message => message.Id == id);
        row.DeletedAtUtc.ShouldNotBeNull();
        row.Body.ShouldBeNull();
    }
}
