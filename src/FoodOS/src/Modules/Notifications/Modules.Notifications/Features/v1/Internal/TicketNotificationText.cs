using System.Globalization;

namespace FSH.Modules.Notifications.Features.v1.Internal;

/// <summary>Follows the API's en/zh translation-table convention; English is the fallback.</summary>
internal static class TicketNotificationText
{
    public static string? Title(string type, CultureInfo culture)
    {
        var chinese = string.Equals(culture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase);
        return type switch
        {
            "tickets.created" => chinese ? "有新工单等待处理" : "A new ticket needs attention",
            "tickets.assigned" => chinese ? "工单处理人已变更" : "Ticket assignment changed",
            "tickets.comment" => chinese ? "工单有新回复" : "A ticket has a new reply",
            "tickets.status-changed" => chinese ? "工单状态已变更" : "Ticket status changed",
            _ => null,
        };
    }
}
