namespace FSH.Modules.Ordering.Domain;

/// <summary>Phone numbers are matched on their digits only, so "+1 (415) 555-0100" equals "14155550100".</summary>
public static class PhoneNumbers
{
    public const int MinSearchDigits = 4;

    public static string Digits(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : new string(value.Where(char.IsAsciiDigit).ToArray());
}
