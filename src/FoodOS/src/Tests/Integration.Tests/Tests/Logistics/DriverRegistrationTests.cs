using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Domain;
using FSH.Modules.Logistics.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Logistics.Contracts.v1.Drivers;
using Mediator;

namespace Integration.Tests.Tests.Logistics;

[Collection(FshCollectionDefinition.Name)]
public sealed class DriverRegistrationTests(FshWebApplicationFactory factory)
{
    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("customer")]
    public async Task Register_Should_RejectInvalidIdentity_WithoutSaving(string kind)
    {
        Guid userId = kind == "missing" ? Guid.NewGuid() : await CreateUserAsync(kind != "inactive", kind == "customer");
        using var client = await new AuthHelper(factory).CreateRootAdminClientAsync();
        using var response = await client.PostAsJsonAsync("/api/v1/logistics/drivers", new { userId, phone = "555-0100" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        using var list = await client.GetAsync("/api/v1/logistics/drivers");
        var drivers = await list.DeserializeAsync<List<DriverDto>>();
        drivers.ShouldNotContain(driver => driver.UserId == userId);
    }

    [Fact]
    public async Task Register_Should_AcceptActiveOperator_AndKeepExistingPhoneOnReplay()
    {
        var userId = await CreateUserAsync(true, false);
        using var client = await new AuthHelper(factory).CreateRootAdminClientAsync();
        using var first = await client.PostAsJsonAsync("/api/v1/logistics/drivers", new { userId, phone = "555-0101" });
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var id = await first.DeserializeAsync<Guid>();
        using var replay = await client.PostAsJsonAsync("/api/v1/logistics/drivers", new { userId, phone = "555-0102" });
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await replay.DeserializeAsync<Guid>()).ShouldBe(id);
        using var list = await client.GetAsync("/api/v1/logistics/drivers");
        var driver = (await list.DeserializeAsync<List<DriverDto>>()).Single(item => item.UserId == userId);
        driver.Phone.ShouldBe("555-0101");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InternalRegistration_Should_RejectMissingOrCustomerContext(bool customer)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(customer ? new AppTenantInfo("driver-customer", "driver-customer") : null);
        var error = await Should.ThrowAsync<CustomException>(async () =>
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CreateDriverCommand(Guid.NewGuid(), "555-0100")));
        error.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RegistrationReplay_Should_RejectUserDeactivatedAfterFirstRegistration()
    {
        var userId = await CreateUserAsync(true, false);
        using var client = await new AuthHelper(factory).CreateRootAdminClientAsync();
        using var first = await client.PostAsJsonAsync("/api/v1/logistics/drivers", new { userId, phone = "555-0101" });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var scope = factory.Services.CreateScope())
        {
            var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync("root");
            scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
            var user = await users.FindByIdAsync(userId.ToString());
            user!.IsActive = false;
            (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        }
        using var replay = await client.PostAsJsonAsync("/api/v1/logistics/drivers", new { userId, phone = "555-0102" });
        replay.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var list = await client.GetAsync("/api/v1/logistics/drivers");
        (await list.DeserializeAsync<List<DriverDto>>()).Single(driver => driver.UserId == userId).Phone.ShouldBe("555-0101");
    }

    private async Task<Guid> CreateUserAsync(bool active, bool customer)
    {
        using var scope = factory.Services.CreateScope();
        var tenant = customer ? new AppTenantInfo("driver-customer", "driver-customer") :
            await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync("root");
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        var handle = "driver-reg-" + Guid.NewGuid().ToString("N");
        var user = new FshUser { UserName = handle, Email = handle + "@example.com", FirstName = "Driver", LastName = "Test", IsActive = active, EmailConfirmed = true };
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>().CreateAsync(user);
        result.Succeeded.ShouldBeTrue(string.Join(",", result.Errors.Select(error => error.Description)));
        return Guid.Parse(user.Id);
    }
}
