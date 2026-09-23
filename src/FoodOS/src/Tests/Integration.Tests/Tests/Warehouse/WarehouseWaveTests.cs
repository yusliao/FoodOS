using FSH.Modules.Inventory.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Warehouse;

/// <summary>
/// External-WMS mode keeps warehouse reads available while every legacy cutoff, wave, assignment and pick write fails closed.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class WarehouseWaveTests(FshWebApplicationFactory factory)
{
    private readonly AuthHelper _auth = new(factory);

    [Fact]
    public async Task WarehouseExecutionEndpoints_Should_FailClosed_WithoutCreatingWaves()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var waveId = Guid.NewGuid();

        await AssertBlockedAsync(client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/warehouses/{warehouse.Id}/cutoff", new { }));
        await AssertBlockedAsync(client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves",
            new { warehouseId = warehouse.Id, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) }));
        await AssertBlockedAsync(client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{waveId}/assign",
            new { pickerUserId = Guid.NewGuid() }));
        await AssertBlockedAsync(client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{waveId}/release", new { }));
        await AssertBlockedAsync(client.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/pick-tasks/{Guid.NewGuid()}/confirm",
            new { scannedLotId = Guid.NewGuid() }));

        using var listed = await client.GetAsync(
            $"{TestConstants.WarehouseBasePath}/waves?warehouseId={warehouse.Id}");
        listed.StatusCode.ShouldBe(HttpStatusCode.OK, await listed.Content.ReadAsStringAsync());
        (await listed.DeserializeAsync<List<WaveDto>>()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ConcurrentAssignments_Should_AllFailClosed_WithoutSelectingWinner()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var waveId = Guid.NewGuid();
        string url = $"{TestConstants.WarehouseBasePath}/waves/{waveId}/assign";
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(url, new { pickerUserId = Guid.NewGuid() }),
            client.PostAsJsonAsync(url, new { pickerUserId = Guid.NewGuid() }));
        try
        {
            responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.Conflict);
            foreach (var response in responses)
                (await response.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }

        using var missing = await client.GetAsync($"{TestConstants.WarehouseBasePath}/waves/{waveId}");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReleaseAndPickRetry_Should_RemainBlocked_WithoutLocalTaskState()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var waveId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await AssertBlockedAsync(client.PostAsJsonAsync(
                $"{TestConstants.WarehouseBasePath}/waves/{waveId}/release", new { }));
            await AssertBlockedAsync(client.PostAsJsonAsync(
                $"{TestConstants.WarehouseBasePath}/pick-tasks/{taskId}/confirm",
                new { scannedLotId = lotId }));
        }

        using var mine = await client.GetAsync($"{TestConstants.WarehouseBasePath}/pick-tasks/mine");
        mine.StatusCode.ShouldBe(HttpStatusCode.OK, await mine.Content.ReadAsStringAsync());
        (await mine.DeserializeAsync<List<PickTaskDto>>()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RepeatedGenerateWave_Should_RemainBlocked_AndReadProjectionStayEmpty()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var warehouse = await CreateWarehouseAsync(client);
        var body = new { warehouseId = warehouse.Id, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) };

        await AssertBlockedAsync(client.PostAsJsonAsync($"{TestConstants.WarehouseBasePath}/waves", body));
        await AssertBlockedAsync(client.PostAsJsonAsync($"{TestConstants.WarehouseBasePath}/waves", body));

        using var listed = await client.GetAsync(
            $"{TestConstants.WarehouseBasePath}/waves?warehouseId={warehouse.Id}");
        listed.StatusCode.ShouldBe(HttpStatusCode.OK, await listed.Content.ReadAsStringAsync());
        (await listed.DeserializeAsync<List<WaveDto>>()).ShouldBeEmpty();
    }

    private static async Task AssertBlockedAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("External WMS confirmation");
    }

    private static async Task<WarehouseDto> CreateWarehouseAsync(HttpClient client)
    {
        var code = $"DC{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var create = await client.PostAsJsonAsync(
            $"{TestConstants.InventoryBasePath}/warehouses",
            new { code, name = $"Pilot {code}", city = "Boston", timeZoneId = (string?)null });
        create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var id = await create.DeserializeAsync<Guid>();

        using var get = await client.GetAsync($"{TestConstants.InventoryBasePath}/warehouses/{id}");
        return await get.DeserializeAsync<WarehouseDto>();
    }
}
