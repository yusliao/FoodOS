using System.Globalization;

namespace FSH.Modules.Notifications.Features.v1.Internal;

internal static class CustomerDeliveryNotificationText
{
    public static string? Title(string type, CultureInfo culture)
    {
        var chinese = string.Equals(culture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase);
        return type switch
        {
            "shop.order-departed" => chinese ? "您的订单已发运" : "Your order has shipped",
            "shop.order-delivered" => chinese ? "您的订单已签收" : "Your order delivery was signed",
            _ => null,
        };
    }
}
