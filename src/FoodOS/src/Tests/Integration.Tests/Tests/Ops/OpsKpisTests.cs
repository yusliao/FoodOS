using FSH.Modules.Ops.Contracts.Dtos;
using FSH.Modules.Ops.Contracts.Authorization;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Ops;

[Collection(FshCollectionDefinition.Name)]
public sealed class OpsKpisTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public OpsKpisTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task GetKpis_Should_Return401_When_Anonymous()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"{TestConstants.OpsBasePath}/kpis?date=2026-09-12");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetKpis_Should_ReturnZeroRates_AndNullTemperature_When_NoActivity()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var response = await client.GetAsync($"{TestConstants.OpsBasePath}/kpis?date=2099-01-01");
        var dto = await response.DeserializeAsync<OpsKpisDto>();

        dto.Date.ShouldBe(new DateOnly(2099, 1, 1));
        dto.FulfillmentRate.ShouldBe(0m);
        dto.StockoutRate.ShouldBe(0m);
        dto.ShrinkageRate.ShouldBe(0m);
        dto.TemperatureComplianceRate.ShouldBeNull();
        dto.CommittedOrderCount.ShouldBe(0);
        dto.FulfilledOrderCount.ShouldBe(0);
    }

    [Fact]
    public async Task RootAdmin_Should_Have_OpsKpisView()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/permissions");
        var permissions = await response.DeserializeAsync<string[]>();
        permissions.ShouldContain(OpsPermissions.Kpis.View);
    }
}
