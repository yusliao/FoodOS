using System.Globalization;

namespace FSH.Framework.Shared.Localization;

/// <summary>
/// Resolves a requested culture onto the configured supported list.
/// </summary>
public static class CultureCatalog
{
    public static bool IsSupported(string? culture, LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(culture))
        {
            return false;
        }

        return options.SupportedCultures.Any(c =>
            string.Equals(c, culture, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Maps <paramref name="requested"/> to a supported culture.
    /// Prefers exact match (es-ES), then language match (es), then default.
    /// </summary>
    public static string Resolve(string? requested, LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string fallback = string.IsNullOrWhiteSpace(options.DefaultCulture)
            ? "en-US"
            : options.DefaultCulture;

        if (string.IsNullOrWhiteSpace(requested))
        {
            return fallback;
        }

        string trimmed = requested.Trim();
        string? exact = options.SupportedCultures.FirstOrDefault(c =>
            string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        string language = trimmed.Split('-', 2)[0];
        string? languageMatch = options.SupportedCultures.FirstOrDefault(c =>
            c.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c, language, StringComparison.OrdinalIgnoreCase));

        return languageMatch ?? fallback;
    }

    public static CultureInfo CreateCulture(string cultureName)
        => CultureInfo.GetCultureInfo(cultureName);
}
