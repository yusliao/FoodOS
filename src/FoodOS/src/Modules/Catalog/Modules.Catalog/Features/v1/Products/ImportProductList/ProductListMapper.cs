using System.Text.RegularExpressions;
using FSH.Modules.Catalog.Domain;

namespace FSH.Modules.Catalog.Features.v1.Products.ImportProductList;

/// <summary>One importable line of the client's product list.</summary>
public sealed record ProductListItem(
    string Sku,
    string Name,
    string Category,
    string BaseUom,
    bool CatchWeight,
    TemperatureZone Zone,
    string SourceUnit,
    string SourceSubUnit);

/// <summary>
/// Maps the client's product list (item numbers grouped by prefix, mixed Chinese/English units) onto catalog
/// fields. The rules were tuned on the real list; operators can still fix individual items in the admin UI.
/// </summary>
public static partial class ProductListMapper
{
    public const string GiftCategory = "赠品";
    public const string OtherCategory = "其他";

    // 甲方编号前两位就是品类段
    private static readonly Dictionary<string, string> CategoryByPrefix = new(StringComparer.Ordinal)
    {
        ["11"] = "禽肉", ["16"] = "禽肉", ["12"] = "牛肉", ["13"] = "猪肉", ["22"] = "海鲜水产",
        ["31"] = "蔬菜水果", ["32"] = "蔬菜水果", ["33"] = "蔬菜水果", ["34"] = "蔬菜水果",
        ["35"] = "豆制品蛋类",
        ["36"] = "面包烘焙", ["74"] = "面包烘焙", ["76"] = "面包烘焙",
        ["38"] = "冷冻预制", ["72"] = "冷冻预制", ["77"] = "冷冻预制", ["79"] = "冷冻预制",
        ["41"] = "食用油", ["42"] = "食用油", ["43"] = "食用油", ["49"] = "食用油",
        ["51"] = "米面粮食", ["52"] = "米面粮食", ["53"] = "米面粮食",
        ["55"] = "调味酱料", ["59"] = "调味酱料", ["60"] = "调味酱料", ["61"] = "调味酱料", ["63"] = "调味酱料",
        ["64"] = "调味酱料", ["65"] = "调味酱料", ["67"] = "调味酱料", ["68"] = "调味酱料", ["69"] = "调味酱料",
        ["92"] = "调味酱料",
        ["62"] = "罐头",
        ["66"] = "饮料", ["97"] = "饮料",
        ["70"] = "面条面皮", ["71"] = "面条面皮", ["93"] = "面条面皮",
        ["94"] = "干货",
        ["81"] = "包装耗材", ["82"] = "包装耗材", ["83"] = "包装耗材", ["84"] = "包装耗材", ["85"] = "包装耗材",
        ["86"] = "包装耗材", ["87"] = "包装耗材", ["88"] = "包装耗材", ["89"] = "包装耗材", ["91"] = "包装耗材",
        ["95"] = "包装耗材", ["96"] = "包装耗材", ["99"] = "包装耗材",
    };

    private static readonly Dictionary<char, string> CategoryByFirstDigit = new()
    {
        ['1'] = "禽肉", ['3'] = "蔬菜水果", ['4'] = "食用油", ['5'] = "米面粮食",
        ['6'] = "调味酱料", ['7'] = "面条面皮", ['8'] = "包装耗材", ['9'] = "调味酱料",
    };

    private static readonly HashSet<string> MeatAndSeafood = new(StringComparer.Ordinal) { "禽肉", "牛肉", "猪肉", "海鲜水产" };

    /// <summary>Returns the item, or null with a reason when the row is a note rather than a product.</summary>
    public static ProductListItem? Map(string itemNo, string name, string unit, string subUnit, out string? skipReason)
    {
        ArgumentNullException.ThrowIfNull(itemNo);
        ArgumentNullException.ThrowIfNull(name);
        skipReason = null;
        string sku = itemNo.Trim().ToUpperInvariant();
        string trimmedName = name.Trim();
        if (sku.Length == 0 || trimmedName.Length == 0)
        {
            skipReason = "编号或名称为空";
            return null;
        }

        if (NoteRow().IsMatch(trimmedName))
        {
            skipReason = "备注行，不是商品";
            return null;
        }

        string category = Category(sku, trimmedName);
        string? baseUom = Uom(unit);
        string? subUom = Uom(subUnit);
        string resolvedUom = baseUom ?? "EA";
        return new ProductListItem(
            sku,
            trimmedName.Length > 200 ? trimmedName[..200] : trimmedName,
            category,
            resolvedUom,
            CatchWeight: subUom == "LB" && resolvedUom != "LB",
            Zone(category, trimmedName),
            unit.Trim(),
            subUnit.Trim());
    }

    public static string Category(string sku, string name)
    {
        ArgumentNullException.ThrowIfNull(sku);
        ArgumentNullException.ThrowIfNull(name);
        if (Gift().IsMatch(name))
        {
            return GiftCategory;
        }

        if (NonFood().IsMatch(name))
        {
            return "包装耗材";
        }

        var digits = LeadingDigits().Match(sku).Value;
        if (digits.Length == 0)
        {
            return OtherCategory;
        }

        // 76AD、76K8 这类「两位数字 + 字母」也按前两位归类
        string category = digits.Length >= 2 && CategoryByPrefix.TryGetValue(digits[..2], out var byPrefix)
            ? byPrefix
            : CategoryByFirstDigit.GetValueOrDefault(digits[0], OtherCategory);

        // 70 段面条里混着冷冻春卷
        return category == "面条面皮" && name.Contains("春卷", StringComparison.Ordinal) ? "冷冻预制" : category;
    }

    /// <summary>Normalizes the dozens of spellings (箱 / 箱CS / CASE / 葙 …) to a short unit code; null if unknown.</summary>
    public static string? Uom(string raw)
    {
        string s = UnitNoise().Replace(raw ?? string.Empty, string.Empty).ToUpperInvariant();
        if (s.Length == 0 || s == "0")
        {
            return null;
        }

        // 「小包(5Pd)」是 5 磅一小包，单位是包；只有不带容器字眼时才算磅
        bool container = ContainsAny(s, "箱", "葙", "CS", "CASE", "包", "袋", "盒", "合", "桶");
        return s switch
        {
            _ when !container && ContainsAny(s, "磅", "LB", "PD") => "LB",
            _ when ContainsAny(s, "箱", "葙", "CS", "CASE") => "CS",
            _ when ContainsAny(s, "桶", "PAIL", "GL") => "PAIL",
            _ when ContainsAny(s, "PC", "PIECE", "塊", "扎", "并", "罐", "缶", "開箱") => "PC",
            _ when ContainsAny(s, "盒", "合", "BOX") => "BOX",
            _ when ContainsAny(s, "包", "袋", "BAG") => "BAG",
            _ when ContainsAny(s, "PACK", "份") => "PACK",
            _ when s.Contains("JUG", StringComparison.Ordinal) => "JUG",
            _ when s.Contains('套', StringComparison.Ordinal) => "SET",
            _ => null,
        };
    }

    public static TemperatureZone Zone(string category, string name)
    {
        if (FrozenWords().IsMatch(name))
        {
            return TemperatureZone.Frozen;
        }

        if (MeatAndSeafood.Contains(category))
        {
            return FreshWords().IsMatch(name) ? TemperatureZone.Chilled : TemperatureZone.Frozen;
        }

        return category switch
        {
            "冷冻预制" => TemperatureZone.Frozen,
            "蔬菜水果" or "豆制品蛋类" => TemperatureZone.Chilled,
            _ => TemperatureZone.Ambient,
        };
    }

    private static bool ContainsAny(string value, params string[] needles)
        => needles.Any(needle => value.Contains(needle, StringComparison.Ordinal));

    [GeneratedRegex(@"PAY FIRST|Order\*{3}", RegexOptions.IgnoreCase)]
    private static partial Regex NoteRow();

    [GeneratedRegex(@"^(送|FREE\b)|\bFree (Poster|Small|Large|Hot)", RegexOptions.IgnoreCase)]
    private static partial Regex Gift();

    // 只认非常明确的非食品字眼，避免「紅蓋」「包菜」「盒裝」这类误伤
    [GeneratedRegex("打印紙|紙巾|手套|保鮮膜|錫紙|空瓶")]
    private static partial Regex NonFood();

    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingDigits();

    [GeneratedRegex(@"[\s\*#@()（）/]")]
    private static partial Regex UnitNoise();

    [GeneratedRegex(@"凍|FROZEN|FROZON|FRZ|IQF", RegexOptions.IgnoreCase)]
    private static partial Regex FrozenWords();

    [GeneratedRegex(@"鮮|FRESH", RegexOptions.IgnoreCase)]
    private static partial Regex FreshWords();
}
