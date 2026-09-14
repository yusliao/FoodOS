using System.Globalization;

namespace FSH.Framework.Web.Localization;

/// <summary>
/// Built-in ProblemDetails copy. Keys are stable; values follow CurrentUICulture (en / zh).
/// </summary>
public static class SharedErrorText
{
    public const string ValidationTitle = "validation_title";
    public const string ValidationDetail = "validation_detail";
    public const string Unauthorized = "unauthorized";
    public const string NotFound = "not_found";
    public const string BadRequest = "bad_request";
    public const string UnexpectedTitle = "unexpected_title";
    public const string UnexpectedDetail = "unexpected_detail";

    public static string Get(string key)
        => Get(key, CultureInfo.CurrentUICulture);

    public static string Get(string key, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(culture);

        string lang = culture.TwoLetterISOLanguageName.ToUpperInvariant();
        if (Table.TryGetValue(lang, out var map) && map.TryGetValue(key, out string? value))
        {
            return value;
        }

        return Table["EN"][key];
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Table = new(StringComparer.Ordinal)
    {
        ["EN"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ValidationTitle] = "Validation error",
            [ValidationDetail] = "One or more validation errors occurred.",
            [Unauthorized] = "Unauthorized",
            [NotFound] = "Not Found",
            [BadRequest] = "Bad Request",
            [UnexpectedTitle] = "An unexpected error occurred",
            [UnexpectedDetail] = "An unexpected error occurred. Please try again later.",
        },
        ["ZH"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ValidationTitle] = "验证错误",
            [ValidationDetail] = "发生了一个或多个验证错误。",
            [Unauthorized] = "未授权",
            [NotFound] = "未找到",
            [BadRequest] = "错误的请求",
            [UnexpectedTitle] = "发生了意外错误",
            [UnexpectedDetail] = "发生了意外错误，请稍后重试。",
        },
    };
}
