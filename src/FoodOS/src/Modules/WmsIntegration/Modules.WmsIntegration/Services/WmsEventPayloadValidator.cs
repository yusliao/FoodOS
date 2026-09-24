using System.Text.Json;
using FSH.Modules.WmsIntegration.Contracts.v1;

namespace FSH.Modules.WmsIntegration.Services;

public static class WmsEventPayloadValidator
{
    public static bool TryValidate(WmsEventEnvelope envelope, out string error)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        error = string.Empty;
        return envelope.EventType switch
        {
            WmsEventTypes.InventorySnapshot or WmsEventTypes.InventoryChanged or WmsEventTypes.InventoryAdjusted =>
                ValidateInventory(envelope, ref error),
            WmsEventTypes.InboundReceived => ValidateLinesEvent(
                envelope, WmsEntityTypes.InboundOrder, "inboundOrderId", "receivedAt", ValidateQuantityLine, ref error),
            WmsEventTypes.InboundQualityCompleted => ValidateLinesEvent(
                envelope, WmsEntityTypes.InboundOrder, "inboundOrderId", "completedAt", ValidateQualityLine, ref error),
            WmsEventTypes.InboundPutawayCompleted => ValidateLinesEvent(
                envelope, WmsEntityTypes.InboundOrder, "inboundOrderId", "completedAt", ValidatePutawayLine, ref error),
            WmsEventTypes.OutboundAllocated or WmsEventTypes.OutboundPicked or WmsEventTypes.OutboundLoaded
                or WmsEventTypes.OutboundShipped => ValidateLinesEvent(
                    envelope, WmsEntityTypes.OutboundOrder, "outboundOrderId", "occurredAt", ValidateQuantityLine, ref error),
            WmsEventTypes.OutboundShortage => ValidateShortage(envelope, ref error),
            WmsEventTypes.ReturnReceived => ValidateReturn(envelope, ref error),
            _ => Fail("unsupported_event_type", ref error),
        };
    }

    private static bool ValidateInventory(WmsEventEnvelope envelope, ref string error)
    {
        if (!ValidateEntity(envelope, WmsEntityTypes.Inventory, ref error)) return false;
        JsonElement payload = envelope.Payload;
        return RequireString(payload, "warehouseId", ref error)
            && RequireString(payload, "ownerId", ref error)
            && RequireString(payload, "sku", ref error)
            && RequireString(payload, "uom", ref error)
            && RequireNonNegativeNumber(payload, "onHandQuantity", ref error)
            && RequireNonNegativeNumber(payload, "allocatedQuantity", ref error)
            && RequireNonNegativeNumber(payload, "availableQuantity", ref error)
            && RequireNonNegativeNumber(payload, "quarantinedQuantity", ref error);
    }

    private static bool ValidateLinesEvent(
        WmsEventEnvelope envelope,
        string entityType,
        string objectProperty,
        string occurredAtProperty,
        LineValidator validateLine,
        ref string error)
    {
        if (!ValidateEntity(envelope, entityType, ref error)) return false;
        JsonElement payload = envelope.Payload;
        return RequireString(payload, objectProperty, ref error)
            && RequireString(payload, "warehouseId", ref error)
            && RequireString(payload, "ownerId", ref error)
            && RequireDateTime(payload, occurredAtProperty, ref error)
            && RequireLines(payload, validateLine, ref error);
    }

    private static bool ValidateShortage(WmsEventEnvelope envelope, ref string error)
    {
        if (!ValidateLinesEvent(
            envelope,
            WmsEntityTypes.OutboundOrder,
            "outboundOrderId",
            "occurredAt",
            ValidateQuantityLine,
            ref error)) return false;
        return RequireString(envelope.Payload, "reasonCode", ref error);
    }

    private static bool ValidateReturn(WmsEventEnvelope envelope, ref string error)
    {
        if (!ValidateLinesEvent(
            envelope,
            WmsEntityTypes.Return,
            "returnId",
            "receivedAt",
            ValidateReturnLine,
            ref error)) return false;
        return RequireString(envelope.Payload, "outboundOrderId", ref error);
    }

    private static bool ValidateEntity(WmsEventEnvelope envelope, string expected, ref string error) =>
        string.Equals(envelope.EntityType, expected, StringComparison.Ordinal)
            || Fail($"entity_type_must_be_{expected}", ref error);

    private static bool RequireLines(JsonElement payload, LineValidator validateLine, ref string error)
    {
        if (!payload.TryGetProperty("lines", out JsonElement lines)
            || lines.ValueKind != JsonValueKind.Array
            || lines.GetArrayLength() == 0
            || lines.GetArrayLength() > 500)
        {
            return Fail("invalid_lines", ref error);
        }

        int index = 0;
        foreach (JsonElement line in lines.EnumerateArray())
        {
            if (line.ValueKind != JsonValueKind.Object || !validateLine(line, ref error))
            {
                if (string.IsNullOrEmpty(error)) error = $"invalid_line_{index}";
                return false;
            }
            index++;
        }
        return true;
    }

    private static bool ValidateQuantityLine(JsonElement line, ref string error) =>
        RequireString(line, "lineId", ref error)
        && RequireString(line, "sku", ref error)
        && RequireString(line, "uom", ref error)
        && RequirePositiveNumber(line, "quantity", ref error);

    private static bool ValidateQualityLine(JsonElement line, ref string error) =>
        RequireString(line, "lineId", ref error)
        && RequireString(line, "sku", ref error)
        && RequireString(line, "uom", ref error)
        && RequireNonNegativeNumber(line, "acceptedQuantity", ref error)
        && RequireNonNegativeNumber(line, "rejectedQuantity", ref error)
        && (GetDecimal(line, "acceptedQuantity") + GetDecimal(line, "rejectedQuantity") > 0
            || Fail("quality_quantity_must_be_positive", ref error));

    private static bool ValidatePutawayLine(JsonElement line, ref string error) =>
        ValidateQuantityLine(line, ref error) && RequireString(line, "locationCode", ref error);

    private static bool ValidateReturnLine(JsonElement line, ref string error) =>
        ValidateQuantityLine(line, ref error) && RequireString(line, "disposition", ref error);

    private static bool RequireString(JsonElement element, string propertyName, ref string error)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString())
            || value.GetString()!.Length > 200)
        {
            return Fail($"invalid_{propertyName}", ref error);
        }
        return true;
    }

    private static bool RequireDateTime(JsonElement element, string propertyName, ref string error)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || !value.TryGetDateTimeOffset(out _))
        {
            return Fail($"invalid_{propertyName}", ref error);
        }
        return true;
    }

    private static bool RequirePositiveNumber(JsonElement element, string propertyName, ref string error) =>
        RequireNumber(element, propertyName, false, ref error);

    private static bool RequireNonNegativeNumber(JsonElement element, string propertyName, ref string error) =>
        RequireNumber(element, propertyName, true, ref error);

    private static bool RequireNumber(
        JsonElement element,
        string propertyName,
        bool allowZero,
        ref string error)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDecimal(out decimal number)
            || (allowZero ? number < 0 : number <= 0))
        {
            return Fail($"invalid_{propertyName}", ref error);
        }
        return true;
    }

    private static decimal GetDecimal(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetDecimal();

    private static bool Fail(string value, ref string error)
    {
        error = value;
        return false;
    }

    private delegate bool LineValidator(JsonElement line, ref string error);
}
