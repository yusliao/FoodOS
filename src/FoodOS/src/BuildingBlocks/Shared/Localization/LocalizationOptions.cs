namespace FSH.Framework.Shared.Localization;

/// <summary>
/// Request culture settings. Default is en-US for EU/US trials.
/// </summary>
public sealed class LocalizationOptions
{
    public const string SectionName = nameof(LocalizationOptions);

    /// <summary>Fallback culture when the request does not match a supported value.</summary>
    public string DefaultCulture { get; set; } = "en-US";

    /// <summary>Cultures the API and UI accept: English and Simplified Chinese.</summary>
    public string[] SupportedCultures { get; set; } = ["en-US", "zh-CN"];
}
