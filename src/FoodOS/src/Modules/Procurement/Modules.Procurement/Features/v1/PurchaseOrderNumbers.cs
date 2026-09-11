using System.Globalization;

namespace FSH.Modules.Procurement.Features.v1;

internal static class PurchaseOrderNumbers
{
    public static string Next(int existingTodayCount, DateTimeOffset utcNow)
    {
        string date = utcNow.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"PO{date}{(existingTodayCount + 1).ToString("0000", CultureInfo.InvariantCulture)}";
    }
}
