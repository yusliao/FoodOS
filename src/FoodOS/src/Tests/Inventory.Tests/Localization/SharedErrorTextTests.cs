using System.Globalization;
using FSH.Framework.Web.Localization;

namespace Inventory.Tests.Localization;

public sealed class SharedErrorTextTests
{
    [Fact]
    public void Get_Should_ReturnEnglishCopy_When_UiCultureIsEn()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");
        SharedErrorText.Get(SharedErrorText.ValidationTitle, culture).ShouldBe("Validation error");
    }

    [Fact]
    public void Get_Should_ReturnChineseCopy_When_UiCultureIsZh()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("zh-CN");
        SharedErrorText.Get(SharedErrorText.NotFound, culture).ShouldBe("未找到");
    }

    [Fact]
    public void Get_Should_FallBackToEnglish_When_CultureIsUnsupported()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("ja-JP");
        SharedErrorText.Get(SharedErrorText.NotFound, culture).ShouldBe("Not Found");
    }
}
