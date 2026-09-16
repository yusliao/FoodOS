using FSH.Framework.Web.Sse;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Sse;

[Collection(FshCollectionDefinition.Name)]
public sealed class SseEndpointTests(FshWebApplicationFactory factory)
{
    [Fact]
    public async Task TokenEndpoint_Should_Require_Authentication_And_Bind_Identity()
    {
        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);
        using var denied = await anonymous.PostAsync("/api/v1/sse/token", null);
        denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var invalid = await anonymous.GetAsync($"/api/v1/sse/stream?token={Guid.NewGuid()}");
        invalid.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var authenticated = await new AuthHelper(factory).CreateRootAdminClientAsync();
        using var issued = await authenticated.PostAsync("/api/v1/sse/token", null);
        issued.StatusCode.ShouldBe(HttpStatusCode.OK);
        issued.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var token = (await issued.DeserializeAsync<TokenResponse>()).Token;
        var principal = await factory.Services.GetRequiredService<ISseTokenService>()
            .ConsumeAsync(token, CancellationToken.None);
        principal.ShouldNotBeNull();
        principal.TenantId.ShouldBe(TestConstants.RootTenantId);
        principal.UserId.ShouldBe((await WaveAssignments.UserIdAsync(authenticated)).ToString());
        using var replay = await anonymous.GetAsync($"/api/v1/sse/stream?token={token}");
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Stream_Should_Isolate_Scopes_Survive_Messages_And_Heartbeat_And_Clean_Up()
    {
        var tokens = factory.Services.GetRequiredService<ISseTokenService>();
        var connections = factory.Services.GetRequiredService<SseConnectionManager>();
        connections.ActiveConnections.ShouldBe(0);
        // Synthetic server-issued identities deliberately share a user id across tenant domains.
        // This verifies transport isolation separately from customer provisioning and business recipient selection.
        var userId = Guid.NewGuid().ToString();
        var otherUser = Guid.NewGuid().ToString();
        var tokenA = await tokens.IssueAsync(userId, "customer-a", CancellationToken.None);
        var tokenB = await tokens.IssueAsync(userId, "customer-b", CancellationToken.None);
        var tokenOther = await tokens.IssueAsync(otherUser, "customer-a", CancellationToken.None);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        using var a = await client.GetAsync($"/api/v1/sse/stream?token={tokenA}&tenant=root&userId={otherUser}",
            HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
        using var b = await client.GetAsync($"/api/v1/sse/stream?token={tokenB}",
            HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
        using var other = await client.GetAsync($"/api/v1/sse/stream?token={tokenOther}",
            HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
        a.StatusCode.ShouldBe(HttpStatusCode.OK);
        a.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        a.Headers.CacheControl!.NoStore.ShouldBeTrue();
        using var readerA = new StreamReader(await a.Content.ReadAsStreamAsync(lifetime.Token));
        using var readerB = new StreamReader(await b.Content.ReadAsStreamAsync(lifetime.Token));
        using var readerOther = new StreamReader(await other.Content.ReadAsStreamAsync(lifetime.Token));
        (await ReadFrameAsync(readerA, lifetime.Token)).ShouldBe(":connected");
        (await ReadFrameAsync(readerB, lifetime.Token)).ShouldBe(":connected");
        (await ReadFrameAsync(readerOther, lifetime.Token)).ShouldBe(":connected");
        connections.ActiveConnections.ShouldBe(3);
        using var replay = await client.GetAsync($"/api/v1/sse/stream?token={tokenA}", lifetime.Token);
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        for (int i = 0; i < 2; i++)
        {
            connections.TrySend("customer-a", userId, new SseEvent("private", $"payload-{i}")).ShouldBe(1);
            (await ReadFrameAsync(readerA, lifetime.Token)).ShouldBe($"event: private\ndata: payload-{i}");
        }
        // Markers are queued after private sends; the first frame must be the marker, not leaked data.
        connections.TrySend("customer-b", userId, new SseEvent("marker", "b")).ShouldBe(1);
        connections.TrySend("customer-a", otherUser, new SseEvent("marker", "other")).ShouldBe(1);
        (await ReadFrameAsync(readerB, lifetime.Token)).ShouldBe("event: marker\ndata: b");
        (await ReadFrameAsync(readerOther, lifetime.Token)).ShouldBe("event: marker\ndata: other");

        (await ReadFrameAsync(readerA, lifetime.Token)).ShouldBe(":heartbeat");
        connections.TrySend("customer-a", userId, new SseEvent("after-heartbeat", "still-open")).ShouldBe(1);
        (await ReadFrameAsync(readerA, lifetime.Token)).ShouldBe("event: after-heartbeat\ndata: still-open");

        // With ResponseHeadersRead the HTTP send has already completed; disposing the response
        // closes the body transport, whereas cancelling its former send token alone does not.
        a.Dispose();
        b.Dispose();
        other.Dispose();
        await lifetime.CancelAsync();
        for (int i = 0; i < 100 && connections.ActiveConnections != 0; i++)
        {
            await Task.Delay(20);
        }
        connections.ActiveConnections.ShouldBe(0);
    }

    private static async Task<string> ReadFrameAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            line.ShouldNotBeNull("The SSE connection must remain open");
            if (line.Length == 0)
            {
                return string.Join('\n', lines);
            }
            lines.Add(line);
        }
    }

    private sealed record TokenResponse(Guid Token);
}
