using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Infrastructure;

internal static class WaveAssignments
{
    public static async Task<Guid> UserIdAsync(HttpClient client)
    {
        using var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        response.EnsureSuccessStatusCode();
        var user = await response.DeserializeAsync<FSH.Modules.Identity.Contracts.DTOs.UserDto>();
        return Guid.Parse(user.Id!);
    }

    public static async Task AssignToSelfAsync(HttpClient supervisor, Guid waveId)
    {
        using var response = await supervisor.PostAsJsonAsync(
            $"{TestConstants.WarehouseBasePath}/waves/{waveId}/assign",
            new { pickerUserId = await UserIdAsync(supervisor) });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
