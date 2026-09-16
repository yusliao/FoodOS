using FSH.Framework.Web.Sse;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Integration.Tests.Tests.Sse;

public sealed class SseRedisTokenTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("valkey/valkey:9.1.0-alpine").Build();

    public Task InitializeAsync() => _redis.StartAsync();
    public Task DisposeAsync() => _redis.DisposeAsync().AsTask();

    [Fact]
    public async Task Token_Should_Be_Consumed_Exactly_Once_Across_Independent_Hosts()
    {
        using var redisA = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        using var redisB = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        await using var hostA = CreateHost(redisA);
        await using var hostB = CreateHost(redisB);
        var tokensA = hostA.GetRequiredService<ISseTokenService>();
        var tokensB = hostB.GetRequiredService<ISseTokenService>();
        var userId = Guid.NewGuid().ToString();
        var token = await tokensA.IssueAsync(userId, "customer-a", CancellationToken.None);
        var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(i =>
            (i % 2 == 0 ? tokensA : tokensB).ConsumeAsync(token, CancellationToken.None)));
        results.Count(p => p is not null).ShouldBe(1);
        results.Single(p => p is not null).ShouldBe(new SsePrincipal(userId, "customer-a"));
        (await tokensA.ConsumeAsync(token, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Token_Should_Expire_In_Redis_And_Not_Fall_Back_To_Local_Copy()
    {
        using var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        await using var host = CreateHost(redis);
        var tokens = host.GetRequiredService<ISseTokenService>();
        var token = await tokens.IssueAsync(Guid.NewGuid().ToString(), "root", CancellationToken.None);
        var key = $"sse:tok:v2:{token:N}";
        var ttl = await redis.GetDatabase().KeyTimeToLiveAsync(key);
        ttl.ShouldNotBeNull();
        ttl.Value.ShouldBeGreaterThan(TimeSpan.Zero);
        ttl.Value.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
        // Force expiry in the isolated test cache without a 30-second wall-clock delay.
        await redis.GetDatabase().KeyExpireAsync(key, TimeSpan.Zero);
        (await tokens.ConsumeAsync(token, CancellationToken.None)).ShouldBeNull();
    }

    private static ServiceProvider CreateHost(IConnectionMultiplexer redis) => new ServiceCollection()
        .AddSingleton(redis)
        .AddHeroSse()
        .BuildServiceProvider();
}
