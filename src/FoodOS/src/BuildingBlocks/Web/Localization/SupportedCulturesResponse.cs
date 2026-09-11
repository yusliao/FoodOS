namespace FSH.Framework.Web.Localization;

public sealed record SupportedCulturesResponse(string DefaultCulture, IReadOnlyList<string> SupportedCultures);
