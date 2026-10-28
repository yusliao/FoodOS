using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.Products;

/// <summary>
/// Shared product text matching over <see cref="Product.SearchText"/>. Every whitespace-separated
/// word must appear somewhere (name, translation, SKU, barcode or pinyin).
/// </summary>
internal static class ProductSearch
{
    private const string Escape = "\\";

    public static string[] Words(string? term) =>
        string.IsNullOrWhiteSpace(term)
            ? []
            : term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length > 64 ? word[..64] : word)
                .Take(5)
                .ToArray();

    public static IQueryable<Product> Match(IQueryable<Product> query, IReadOnlyList<string> words)
    {
        foreach (string word in words)
        {
            string pattern = "%" + EscapeLike(word) + "%";
            query = query.Where(p => EF.Functions.ILike(p.SearchText, pattern, Escape));
        }

        return query;
    }

    /// <summary>Exact SKU/barcode first, then products where a token starts with the first word, then the rest.</summary>
    public static IOrderedQueryable<Product> OrderByRelevance(IQueryable<Product> query, IReadOnlyList<string> words)
    {
        string first = words[0];
        string upper = first.ToUpperInvariant();
        string tokenPrefix = "% " + EscapeLike(first) + "%";
        return query
            .OrderByDescending(p => p.Sku == upper || p.Barcode == first)
            .ThenByDescending(p => EF.Functions.ILike(p.SearchText, tokenPrefix, Escape))
            .ThenBy(p => p.Name.Length)
            .ThenBy(p => p.Name)
            .ThenBy(p => p.Id);
    }

    private static string EscapeLike(string value) => value
        .Replace(Escape, Escape + Escape, StringComparison.Ordinal)
        .Replace("%", Escape + "%", StringComparison.Ordinal)
        .Replace("_", Escape + "_", StringComparison.Ordinal);
}
