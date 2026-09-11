using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts;

namespace FSH.Modules.Procurement.Features.v1;

internal static class ZoneKinds
{
    public static TemperatureZoneKind Parse(string zone)
    {
        if (Enum.TryParse<TemperatureZoneKind>(zone, ignoreCase: true, out var kind))
        {
            return kind;
        }

        throw new CustomException(
            $"Unknown temperature zone '{zone}'.",
            (IEnumerable<string>?)null,
            HttpStatusCode.BadRequest);
    }
}
