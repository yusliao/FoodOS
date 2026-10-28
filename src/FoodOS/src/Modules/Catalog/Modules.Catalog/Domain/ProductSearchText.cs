using System.Text;
using ToolGood.Words.Pinyin;

namespace FSH.Modules.Catalog.Domain;

/// <summary>
/// Builds the denormalized text that product search matches against: SKU, barcode, every name
/// (base and translations) and, for Chinese names, full pinyin plus pinyin initials. Tokens are
/// separated by a space and the text starts with a space, so <c>"% " + term + "%"</c> means
/// "a token starts with the term". Matching is done with ILIKE, so the text is stored upper-case.
/// </summary>
public static class ProductSearchText
{
    public const int MaxLength = 2048;

    public static string Build(string sku, string? barcode, string name, IEnumerable<string> translatedNames)
    {
        ArgumentNullException.ThrowIfNull(translatedNames);
        var tokens = new List<string>();
        AddToken(tokens, sku);
        AddToken(tokens, barcode);
        foreach (string candidate in translatedNames.Prepend(name).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddToken(tokens, candidate);
            if (!string.IsNullOrWhiteSpace(candidate) && WordsHelper.HasChinese(candidate))
            {
                // "三文鱼" -> "SanWenYu" and "SWY", so both "sanwen" and "swy" match.
                AddToken(tokens, WordsHelper.GetPinyin(candidate, false));
                AddToken(tokens, WordsHelper.GetFirstPinyin(candidate));
            }
        }

        var text = new StringBuilder(" ");
        foreach (string token in tokens.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (text.Length + token.Length + 1 > MaxLength) break;
            text.Append(token).Append(' ');
        }

        return text.ToString().ToUpperInvariant();
    }

    private static void AddToken(List<string> tokens, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        tokens.Add(string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
    }
}
