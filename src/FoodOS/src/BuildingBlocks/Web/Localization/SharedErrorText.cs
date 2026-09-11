using System.Globalization;

namespace FSH.Framework.Web.Localization;

/// <summary>
/// Built-in ProblemDetails copy for P0 cultures. Keys are stable; values follow CurrentUICulture.
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
        ["ES"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ValidationTitle] = "Error de validación",
            [ValidationDetail] = "Se han producido uno o más errores de validación.",
            [Unauthorized] = "No autorizado",
            [NotFound] = "No encontrado",
            [BadRequest] = "Solicitud incorrecta",
            [UnexpectedTitle] = "Se ha producido un error inesperado",
            [UnexpectedDetail] = "Se ha producido un error inesperado. Inténtelo de nuevo más tarde.",
        },
        ["FR"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ValidationTitle] = "Erreur de validation",
            [ValidationDetail] = "Une ou plusieurs erreurs de validation se sont produites.",
            [Unauthorized] = "Non autorisé",
            [NotFound] = "Introuvable",
            [BadRequest] = "Requête incorrecte",
            [UnexpectedTitle] = "Une erreur inattendue s'est produite",
            [UnexpectedDetail] = "Une erreur inattendue s'est produite. Veuillez réessayer plus tard.",
        },
        ["DE"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ValidationTitle] = "Validierungsfehler",
            [ValidationDetail] = "Mindestens ein Validierungsfehler ist aufgetreten.",
            [Unauthorized] = "Nicht autorisiert",
            [NotFound] = "Nicht gefunden",
            [BadRequest] = "Ungültige Anforderung",
            [UnexpectedTitle] = "Ein unerwarteter Fehler ist aufgetreten",
            [UnexpectedDetail] = "Ein unerwarteter Fehler ist aufgetreten. Bitte versuchen Sie es später erneut.",
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
