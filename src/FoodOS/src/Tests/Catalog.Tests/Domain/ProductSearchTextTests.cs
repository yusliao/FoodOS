using FSH.Modules.Catalog.Domain;

namespace Catalog.Tests.Domain;

public sealed class ProductSearchTextTests
{
    [Fact]
    public void Build_Should_Include_Sku_Barcode_Names_And_Pinyin_UpperCased()
    {
        string text = ProductSearchText.Build("sal-001", "6901234567890", "Atlantic Salmon", ["三文鱼刺身"]);

        text.ShouldStartWith(" ");
        text.ShouldContain(" SAL-001 ");
        text.ShouldContain(" 6901234567890 ");
        text.ShouldContain(" ATLANTIC SALMON ");
        text.ShouldContain(" 三文鱼刺身 ");
        text.ShouldContain(" SANWENYUCISHEN ");
        text.ShouldContain(" SWYCS ");
        text.ShouldBe(text.ToUpperInvariant());
    }

    [Fact]
    public void Build_Should_Use_The_Common_Reading_Of_Polyphonic_Characters()
    {
        ProductSearchText.Build("CQ-1", null, "重庆小面", []).ShouldContain(" CHONGQINGXIAOMIAN ");
    }

    [Fact]
    public void Build_Should_Not_Add_Pinyin_For_Names_Without_Chinese()
    {
        string text = ProductSearchText.Build("BEEF-1", null, "Beef Brisket", ["Beef Brisket"]);

        text.ShouldBe(" BEEF-1 BEEF BRISKET ");
    }

    [Fact]
    public void Build_Should_Stay_Within_The_Column_Length()
    {
        string longName = new('鱼', 900);

        ProductSearchText.Build("LONG-1", null, longName, [longName + "汤"]).Length
            .ShouldBeLessThanOrEqualTo(ProductSearchText.MaxLength);
    }

    [Fact]
    public void Product_Should_Refresh_SearchText_When_Names_Or_Barcode_Change()
    {
        var product = Product.Create("sku-9", "Shrimp", null, Guid.NewGuid(), Guid.NewGuid(), new Money(1m, "USD"), 0);
        product.SearchText.ShouldContain(" SHRIMP ");

        product.UpsertTranslation("zh-CN", "虾仁", null);
        product.SearchText.ShouldContain(" XR ");

        product.SetFulfillmentAttributes(TemperatureZone.Frozen, null, 0, "kg", false, "123456789", null);
        product.SearchText.ShouldContain(" 123456789 ");

        product.Update("Peeled Shrimp", null, product.BrandId, product.CategoryId, isActive: true);
        product.SearchText.ShouldContain(" PEELED SHRIMP ");
        product.SearchText.ShouldContain(" XIAREN ");
    }
}
