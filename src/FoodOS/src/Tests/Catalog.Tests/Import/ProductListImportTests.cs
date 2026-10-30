using FSH.Modules.Catalog.Domain;
using FSH.Modules.Catalog.Features.v1.Products.ImportProductList;

namespace Catalog.Tests.Import;

public sealed class ProductListImportTests
{
    [Theory]
    [InlineData("1141", "無骨胸<49222/49226> Boneless Breast", "禽肉")]
    [InlineData("121", "牛(球)條(肩膀) 12420/12440 Beef Chuck Mock Tenders", "牛肉")]
    [InlineData("22112199", "21/25南美蝦 50#(CROWN) HL/SO SHRIMP", "海鲜水产")]
    [InlineData("224012A", "(開箱)(5磅)干貝 10/20 (黑珍珠)", "海鲜水产")]
    [InlineData("313", "(黃)大洋蔥 Onion (Jumbo Yellow)", "蔬菜水果")]
    [InlineData("82041", "(4FC6) 4OZ 湯凈杯 (CONVERMEX) 4OZ FOAM CONTAINERS", "包装耗材")]
    [InlineData("7002091", "(東味) 3OZ 蝦春卷 3OZ SHRIMP EGG ROLL", "冷冻预制")]
    [InlineData("70020", "(PAD THAI)泰國鮮河粉 5/10LB (PAD THAI) RICE NOODLE", "面条面皮")]
    [InlineData("1101", "信用卡卷裝打印紙 (50 roll) 80mm x 70mt", "包装耗材")]
    [InlineData("98006", "送海報 雞肉芝士春卷海報 Free Poster Chicken Cheese Eggroll", "赠品")]
    [InlineData("514073", "FREE (大箱)(袋裝)海天雞精 6/5LBS", "赠品")]
    [InlineData("1125041", "紅蓋大胸(非固定磅) <8888>Roaster Breast CW", "禽肉")]
    [InlineData("314", "包 菜 CABBAGE", "蔬菜水果")]
    [InlineData("76AD", "ACC 蛋糕", "面包烘焙")]
    [InlineData("ABC", "unknown", "其他")]
    public void Category_Should_FollowItemNumberPrefix_WithNameOverrides(string sku, string name, string expected)
        => ProductListMapper.Category(sku, name).ShouldBe(expected);

    [Theory]
    [InlineData("箱", "CS")]
    [InlineData("箱CS", "CS")]
    [InlineData("case", "CS")]
    [InlineData("葙", "CS")]
    [InlineData("箱 (CASE)", "CS")]
    [InlineData("磅", "LB")]
    [InlineData("磅/PD", "LB")]
    [InlineData("lb", "LB")]
    [InlineData("包BAG", "BAG")]
    [InlineData("小包(5Pd)", "BAG")]
    [InlineData("包/PC", "PC")]
    [InlineData("***缶piece", "PC")]
    [InlineData("##(合)", "BOX")]
    [InlineData("桶PAIL", "PAIL")]
    [InlineData("份", "PACK")]
    [InlineData("套", "SET")]
    [InlineData("JUG", "JUG")]
    public void Uom_Should_NormalizeClientSpellings(string raw, string expected)
        => ProductListMapper.Uom(raw).ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("??")]
    public void Uom_Should_ReturnNull_When_Unknown(string raw) => ProductListMapper.Uom(raw).ShouldBeNull();

    [Fact]
    public void Map_Should_FlagCatchWeight_When_SoldByCaseButPricedByPound()
    {
        var item = ProductListMapper.Map("113120", "雞腿(4013/7650)(非固定磅) Jumbo Whole leg", "箱", "磅", out _)!;

        item.BaseUom.ShouldBe("CS");
        item.CatchWeight.ShouldBeTrue();
        item.Category.ShouldBe("禽肉");
        item.Zone.ShouldBe(TemperatureZone.Frozen);
    }

    [Fact]
    public void Map_Should_KeepOriginalName_And_UppercaseSku()
    {
        var item = ProductListMapper.Map(" 229030a ", "(開箱) 煙熏日本三文魚A級 ", "PC", "磅/LB", out _)!;

        item.Sku.ShouldBe("229030A");
        item.Name.ShouldBe("(開箱) 煙熏日本三文魚A級");
        item.CatchWeight.ShouldBeTrue();
    }

    [Theory]
    [InlineData("0", "先收款後下貨!!! PAY FIRST!!!")]
    [InlineData("125", "****  有福華 *** **** Hang Seng Seafood Order***")]
    [InlineData("", "名称")]
    public void Map_Should_SkipNoteRows(string itemNo, string name)
    {
        ProductListMapper.Map(itemNo, name, "cs", "cs", out string? reason).ShouldBeNull();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("禽肉", "(寶山) 無骨胸 Boneless Breast", TemperatureZone.Frozen)]
    [InlineData("禽肉", "<2950>中雞翅 (2950) Fresh Med Wing", TemperatureZone.Chilled)]
    [InlineData("海鲜水产", "南美蝦 50#", TemperatureZone.Frozen)]
    [InlineData("蔬菜水果", "羅馬生菜 LETTUCE", TemperatureZone.Chilled)]
    [InlineData("冷冻预制", "炸芝士條", TemperatureZone.Frozen)]
    [InlineData("米面粮食", "巧克力雪糕粉 Chocolate Ice Cream Mix", TemperatureZone.Ambient)]
    [InlineData("干货", "(凍)something", TemperatureZone.Frozen)]
    [InlineData("调味酱料", "海天蠔油", TemperatureZone.Ambient)]
    public void Zone_Should_DefaultByCategory_And_RespectFrozenOrFreshWords(string category, string name, TemperatureZone expected)
        => ProductListMapper.Zone(category, name).ShouldBe(expected);

    [Fact]
    public void Reader_Should_ReadSharedStringsNumbersAndSparseCells()
    {
        var bytes = XlsxTestWorkbook.Build(
            ["編號_x000D_\nItem No.", " ", "名稱_x000D_\nItem Name", "單位_x000D_\nUnit"],
            [1125041, 0, "紅蓋大胸 Roaster Breast", "箱"],
            ["224012A", 0, "干貝 & <Scallop>", "包/PC"]);

        var rows = XlsxSheetReader.ReadFirstSheet(bytes);

        rows.Count.ShouldBe(3);
        rows[0][0].ShouldBe("編號\nItem No.");
        rows[1][0].ShouldBe("1125041");
        rows[1][2].ShouldBe("紅蓋大胸 Roaster Breast");
        rows[2][0].ShouldBe("224012A");
        rows[2][2].ShouldBe("干貝 & <Scallop>");
    }
}
