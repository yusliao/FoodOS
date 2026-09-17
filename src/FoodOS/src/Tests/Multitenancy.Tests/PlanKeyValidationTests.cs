using FSH.Framework.Persistence;
using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Multitenancy.Contracts.v1.CreateTenant;
using FSH.Modules.Multitenancy.Contracts.v1.RenewTenant;
using FSH.Modules.Multitenancy.Features.v1.CreateTenant;
using FSH.Modules.Multitenancy.Features.v1.RenewTenant;
using NSubstitute;

namespace Multitenancy.Tests;

public sealed class PlanKeyValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("x")]
    [InlineData("0")]
    [InlineData("pro-yearly")]
    [InlineData("Legacy_Plan")]
    [InlineData(" plan with spaces ")]
    public async Task CreateAndRenew_AcceptCatalogKeysOrDefault(string? key)
    {
        await AssertValidAsync(key, true);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public async Task CreateAndRenew_EnforceCatalogKeyLength(int length, bool valid)
    {
        await AssertValidAsync(new string('x', length), valid);
    }

    private static async Task AssertValidAsync(string? key, bool valid)
    {
        var create = new CreateTenantCommandValidator(
            Substitute.For<ITenantService>(), Substitute.For<IConnectionStringValidator>());
        var result = await create.ValidateAsync(new CreateTenantCommand(
            "test", "Test", null, "admin@test.example", "Password123!", "test", key));
        result.IsValid.ShouldBe(valid);
        var renew = new RenewTenantCommandValidator();
        var renewal = await renew.ValidateAsync(new RenewTenantCommand("test", key));
        renewal.IsValid.ShouldBe(valid);
    }
}
