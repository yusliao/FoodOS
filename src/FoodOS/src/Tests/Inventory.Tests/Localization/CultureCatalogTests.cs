using FSH.Framework.Shared.Localization;

namespace Inventory.Tests.Localization;

public sealed class CultureCatalogTests
{
    private static readonly LocalizationOptions Options = new()
    {
        DefaultCulture = "en-US",
        SupportedCultures = ["en-US", "zh-CN"]
    };

    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh", "zh-CN")]
    [InlineData("zh-TW", "zh-CN")]
    [InlineData("en", "en-US")]
    [InlineData("en-GB", "en-US")]
    [InlineData("es-ES", "en-US")]
    [InlineData("ja-JP", "en-US")]
    [InlineData(null, "en-US")]
    public void Resolve_Should_MapToSupportedOrDefault(string? requested, string expected)
    {
        CultureCatalog.Resolve(requested, Options).ShouldBe(expected);
    }
}
