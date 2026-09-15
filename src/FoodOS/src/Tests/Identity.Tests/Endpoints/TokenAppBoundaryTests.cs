using FSH.Modules.Identity.Features.v1.Tokens.TokenGeneration;

namespace Identity.Tests.Endpoints;

public sealed class TokenAppBoundaryTests
{
    [Theory]
    [InlineData("root", "dashboard", true)]
    [InlineData("ROOT", "DASHBOARD", true)]
    [InlineData("restaurant-a", "admin", true)]
    [InlineData("root", "admin", false)]
    [InlineData("restaurant-a", "dashboard", false)]
    [InlineData("restaurant-a", null, false)]
    public void GetAppBoundaryError_Should_Enforce_Operator_And_Customer_App_Separation(
        string tenant,
        string? app,
        bool shouldReject)
    {
        var error = GenerateTokenEndpoint.GetAppBoundaryError(tenant, app);

        (error is not null).ShouldBe(shouldReject);
    }
}
