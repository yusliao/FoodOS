using FSH.Modules.Notifications.Contracts.v1.DTOs;
using FSH.Modules.Notifications.Domain;
using System.Globalization;

namespace FSH.Modules.Notifications.Features.v1.Internal;

internal static class NotificationMappers
{
    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.Type, TicketNotificationText.Title(n.Type, CultureInfo.CurrentUICulture)
            ?? CustomerDeliveryNotificationText.Title(n.Type, CultureInfo.CurrentUICulture) ?? n.Title,
            n.Body, n.Link, n.Source, n.MetadataJson, n.ReadAtUtc, n.CreatedAtUtc);
}
