using FSH.Framework.Shared.Localization;

namespace Inventory.Tests.Localization;

public sealed class CultureCatalogTests
{
    private static readonly LocalizationOptions Options = new()
    {
        DefaultCulture = "en-US",
        SupportedCultures = ["en-US", "es-ES", "fr-FR", "de-DE", "zh-CN"]
    };

    [Theory]
    [InlineData("es-ES", "es-ES")]
    [InlineData("es", "es-ES")]
    [InlineData("fr-CA", "fr-FR")]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh", "zh-CN")]
    [InlineData("zh-TW", "zh-CN")]
    [InlineData("ja-JP", "en-US")]
    [InlineData(null, "en-US")]
    public void Resolve_Should_MapToSupportedOrDefault(string? requested, string expected)
    {
        CultureCatalog.Resolve(requested, Options).ShouldBe(expected);
    }
}
