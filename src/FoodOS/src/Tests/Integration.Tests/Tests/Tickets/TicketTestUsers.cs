using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Tickets;

internal static class TicketTestUsers
{
    public static async Task<Guid> CreateAssigneeAsync(HttpClient operatorAdmin)
    {
        string name = $"support{Guid.NewGuid():N}";
        using var response = await operatorAdmin.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/register", new
        {
            firstName = "Support", lastName = "Test", email = $"{name}@test.com", userName = name,
            password = TestConstants.DefaultPassword, confirmPassword = TestConstants.DefaultPassword,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return Guid.Parse((await response.DeserializeAsync<RegisteredUser>()).UserId);
    }

    private sealed record RegisteredUser(string UserId);
}
