using FSH.Modules.Ops.Contracts.Authorization;
using FSH.Modules.Ops.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Ops;

[Collection(FshCollectionDefinition.Name)]
public sealed class OpsLotTraceTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public OpsLotTraceTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task GetLotTrace_Should_Return401_When_Anonymous()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"{TestConstants.OpsBasePath}/lots/{Guid.CreateVersion7()}/trace");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLotTrace_Should_ReturnEmpty_When_LotHasNoEvents()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var lotId = Guid.CreateVersion7();
        var response = await client.GetAsync($"{TestConstants.OpsBasePath}/lots/{lotId}/trace");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var dto = await response.DeserializeAsync<LotTraceDto>();
        dto.LotId.ShouldBe(lotId);
        dto.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task RootAdmin_Should_Have_OpsTraceView()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/permissions");
        var permissions = await response.DeserializeAsync<string[]>();
        permissions.ShouldContain(OpsPermissions.Trace.View);
    }
}
