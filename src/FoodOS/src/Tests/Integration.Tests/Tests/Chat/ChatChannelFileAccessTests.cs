using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Files.Contracts.v1.DTOs;
using FSH.Modules.Identity.Domain;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.Tests.Chat;

[Collection(FshCollectionDefinition.Name)]
public sealed class ChatChannelFileAccessTests
{
    private const string ChatBasePath = "/api/v1/chat";
    private const string FilesBasePath = "/api/v1/files";

    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public ChatChannelFileAccessTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task ChatAttachment_Should_Reject_PublicUpload_And_VisibilityChange()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        Guid channelId = await CreateChannelAsync(client, Unique("PrivateOnly"));
        object Upload(int visibility) => new
        {
            ownerType = "ChatChannel", ownerId = channelId, fileName = "note.txt",
            contentType = "text/plain", sizeBytes = 32, visibility, category = "Document",
        };
        using var publicUpload = await client.PostAsJsonAsync($"{FilesBasePath}/upload-url", Upload(0));
        publicUpload.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var upload = await client.PostAsJsonAsync($"{FilesBasePath}/upload-url", Upload(1));
        upload.StatusCode.ShouldBe(HttpStatusCode.OK);
        var file = await upload.DeserializeAsync<FSH.Modules.Files.Contracts.v1.DTOs.PresignedUploadResponse>();
        using var publish = await client.PatchAsJsonAsync($"{FilesBasePath}/{file.FileAssetId}/visibility", new { visibility = 0 });
        publish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequestUploadUrl_For_ChatChannel_Should_Succeed_For_Member()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(client, Unique("AttachOk"));

        using var response = await client.PostAsJsonAsync($"{FilesBasePath}/upload-url", new
        {
            ownerType = "ChatChannel",
            ownerId = channelId,
            fileName = "note.txt",
            contentType = "text/plain",
            sizeBytes = 32,
            visibility = 1, // Private
            category = "Document",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequestUploadUrl_For_ChatChannel_Should_Return_403_For_NonMember()
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(adminClient, Unique("Forbidden"));

        var (_, bobEmail, bobPassword) = await RegisterAndConfirmAsync(adminClient, "bob");
        using var bobClient = await _auth.CreateAuthenticatedClientAsync(bobEmail, bobPassword);

        using var response = await bobClient.PostAsJsonAsync($"{FilesBasePath}/upload-url", new
        {
            ownerType = "ChatChannel",
            ownerId = channelId,
            fileName = "note.txt",
            contentType = "text/plain",
            sizeBytes = 32,
            visibility = 1,
            category = "Document",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequestUploadUrl_For_ChatChannel_Should_Return_403_When_OwnerId_Missing()
    {
        // Policy requires a channel ownerId — without it CanAttachAsync returns false.
        using var client = await _auth.CreateRootAdminClientAsync();

        using var response = await client.PostAsJsonAsync($"{FilesBasePath}/upload-url", new
        {
            ownerType = "ChatChannel",
            ownerId = (Guid?)null,
            fileName = "note.txt",
            contentType = "text/plain",
            sizeBytes = 32,
            visibility = 1,
            category = "Document",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DownloadUrl_For_ChatAttachment_Should_Follow_Current_Channel_Membership()
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var channelId = await CreateChannelAsync(adminClient, Unique("ReadPolicy"));
        var fileAssetId = await UploadAttachmentAsync(adminClient, channelId);
        var (bobId, bobEmail, bobPassword) = await RegisterAndConfirmAsync(adminClient, "reader");
        using var bobClient = await _auth.CreateAuthenticatedClientAsync(bobEmail, bobPassword);

        using var beforeJoin = await bobClient.GetAsync($"{FilesBasePath}/{fileAssetId}/url");
        beforeJoin.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var add = await adminClient.PostAsJsonAsync($"{ChatBasePath}/channels/{channelId}/members", new { userIds = new[] { bobId } });
        add.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var joined = await bobClient.GetAsync($"{FilesBasePath}/{fileAssetId}/url");
        joined.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var remove = await adminClient.DeleteAsync($"{ChatBasePath}/channels/{channelId}/members/{bobId}");
        remove.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var afterRemoval = await bobClient.GetAsync($"{FilesBasePath}/{fileAssetId}/url");
        afterRemoval.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ─── helpers ─────────────────────────────────────────────────────

    private static string Unique(string prefix) => $"chat-{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private static async Task<Guid> CreateChannelAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync($"{ChatBasePath}/channels", new
        {
            name,
            description = (string?)null,
            isPrivate = false,
        });
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> UploadAttachmentAsync(HttpClient client, Guid channelId)
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        using var request = await client.PostAsJsonAsync($"{FilesBasePath}/upload-url", new
        {
            ownerType = "ChatChannel",
            ownerId = channelId,
            fileName = "note.txt",
            contentType = "text/plain",
            sizeBytes = bytes.Length,
            visibility = 1,
            category = "Document",
        });
        request.StatusCode.ShouldBe(HttpStatusCode.OK);
        var presigned = await request.DeserializeAsync<PresignedUploadResponse>();
        using var raw = new HttpClient();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var put = await raw.PutAsync(presigned.UploadUrl, content);
        put.EnsureSuccessStatusCode();
        using var finalize = await client.PostAsync($"{FilesBasePath}/{presigned.FileAssetId}/finalize", null);
        finalize.StatusCode.ShouldBe(HttpStatusCode.OK);
        return presigned.FileAssetId;
    }

    private async Task<(string id, string email, string password)> RegisterAndConfirmAsync(HttpClient adminClient, string prefix)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var email = $"{prefix}-{unique}@example.com";
        var userName = $"{prefix}{unique}";
        const string password = "Test@1234!";

        using var response = await adminClient.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/register", new
        {
            firstName = prefix,
            lastName = "Test",
            email,
            userName,
            password,
            confirmPassword = password,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var registered = await response.DeserializeAsync<RegisterResult>();

        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var user = await userManager.FindByIdAsync(registered.UserId);
        user.ShouldNotBeNull();
        if (!user!.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            (await userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        }

        return (registered.UserId, email, password);
    }
}
