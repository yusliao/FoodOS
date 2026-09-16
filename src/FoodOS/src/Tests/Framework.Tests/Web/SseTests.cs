using FSH.Framework.Web.Sse;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Framework.Tests.Web;

public sealed class SseTests
{
    [Fact]
    public async Task RedisFailure_Should_Not_Fall_Back_To_Memory_Token()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        database.StringGetDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromException<RedisValue>(new TimeoutException("Isolated cache failure")));
        await using var services = new ServiceCollection().AddSingleton(redis).AddHeroSse().BuildServiceProvider();
        var token = Guid.NewGuid();
        services.GetRequiredService<IMemoryCache>().Set($"sse:tok:v2:{token:N}",
            new SsePrincipal(Guid.NewGuid().ToString(), "root"));
        await Should.ThrowAsync<TimeoutException>(() => services.GetRequiredService<ISseTokenService>()
            .ConsumeAsync(token, CancellationToken.None));
    }

    [Fact]
    public async Task MemoryToken_Should_Have_Exactly_One_Concurrent_Consumer_Across_Scopes()
    {
        await using var services = new ServiceCollection().AddHeroSse().BuildServiceProvider();
        using var firstScope = services.CreateScope();
        using var secondScope = services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ISseTokenService>();
        var second = secondScope.ServiceProvider.GetRequiredService<ISseTokenService>();
        first.ShouldBeSameAs(second);
        var user = Guid.NewGuid().ToString();
        var token = await first.IssueAsync(user, "customer-a", CancellationToken.None);
        var consumers = Enumerable.Range(0, 64)
            .Select(i => Task.Run(() => (i % 2 == 0 ? first : second).ConsumeAsync(token, CancellationToken.None)));
        var results = await Task.WhenAll(consumers);
        results.Count(p => p is not null).ShouldBe(1);
        results.Single(p => p is not null).ShouldBe(new SsePrincipal(user, "customer-a"));
        (await first.ConsumeAsync(token, CancellationToken.None)).ShouldBeNull();
        (await first.ConsumeAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
        (await first.ConsumeAsync(Guid.Empty, CancellationToken.None)).ShouldBeNull();
    }

    [Theory]
    [InlineData("", "customer-a")]
    [InlineData("not-a-user", "customer-a")]
    [InlineData("00000000-0000-0000-0000-000000000000", "customer-a")]
    [InlineData("00000000-0000-0000-0000-000000000001", "")]
    [InlineData("00000000-0000-0000-0000-000000000001", null)]
    public async Task Token_Should_Reject_Missing_Identity(string userId, string? tenantId)
    {
        await using var services = new ServiceCollection().AddHeroSse().BuildServiceProvider();
        await Should.ThrowAsync<ArgumentException>(() => services.GetRequiredService<ISseTokenService>()
            .IssueAsync(userId, tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task Cancelled_Consume_Should_Not_Use_Token()
    {
        await using var services = new ServiceCollection().AddHeroSse().BuildServiceProvider();
        var tokens = services.GetRequiredService<ISseTokenService>();
        var token = await tokens.IssueAsync(Guid.NewGuid().ToString(), "root", CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => tokens.ConsumeAsync(token, cancelled.Token));
        (await tokens.ConsumeAsync(token, CancellationToken.None)).ShouldNotBeNull();
    }

    [Fact]
    public void TargetedSend_Should_Match_Tenant_And_User_And_All_Their_Tabs()
    {
        var manager = new SseConnectionManager(NullLogger<SseConnectionManager>.Instance);
        var a = manager.Connect("same-user", "customer-a");
        var aSecondTab = manager.Connect("same-user", "customer-a");
        var b = manager.Connect("same-user", "customer-b");
        var other = manager.Connect("other-user", "customer-a");
        var message = new SseEvent("test", "private-payload");
        manager.TrySend("customer-a", "same-user", message).ShouldBe(2);
        a.Reader.TryRead(out var received).ShouldBeTrue();
        received.ShouldBe(message);
        aSecondTab.Reader.TryRead(out _).ShouldBeTrue();
        b.Reader.TryRead(out _).ShouldBeFalse();
        other.Reader.TryRead(out _).ShouldBeFalse();
        manager.Disconnect(a.ConnectionId);
        manager.Disconnect(aSecondTab.ConnectionId);
        manager.TrySend("customer-a", "same-user", message).ShouldBe(0);
        manager.ActiveConnections.ShouldBe(2);
        manager.Broadcast("customer-b", message).ShouldBe(1);
        b.Reader.TryRead(out _).ShouldBeTrue();
        other.Reader.TryRead(out _).ShouldBeFalse();
        manager.Disconnect(b.ConnectionId);
        manager.Disconnect(other.ConnectionId);
        manager.ActiveConnections.ShouldBe(0);
    }

    [Fact]
    public void Connections_And_Sends_Should_Reject_Empty_Scope()
    {
        var manager = new SseConnectionManager(NullLogger<SseConnectionManager>.Instance);
        var message = new SseEvent("test", "payload");
        Should.Throw<ArgumentException>(() => manager.Connect("", "root"));
        Should.Throw<ArgumentException>(() => manager.Connect("user", ""));
        Should.Throw<ArgumentException>(() => manager.TrySend("", "user", message));
        Should.Throw<ArgumentException>(() => manager.Broadcast("", message));
        manager.ActiveConnections.ShouldBe(0);
    }
}
