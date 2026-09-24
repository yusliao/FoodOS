using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
using FSH.Modules.WmsIntegration.Data;
using FSH.Modules.WmsIntegration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Services;

public sealed class WmsOrderNotificationGateway(
    WmsIntegrationDbContext db,
    IWmsStandardClient client,
    IOptions<WmsIntegrationOptions> options,
    TimeProvider clock) : IWmsOrderNotificationGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WmsOrderNotificationResult> SubmitAsync(
        string idempotencyKey,
        string correlationId,
        WmsOutboundOrderNotice notice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notice);
        if (notice.OrderId == Guid.Empty || notice.Lines.Count == 0 || notice.Lines.Any(x => x.Quantity <= 0))
        {
            throw new CustomException(
                "An outbound order notification requires an order and positive lines.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        JsonElement payload;
        try
        {
            payload = await BuildSubmitPayloadAsync(notice, cancellationToken).ConfigureAwait(false);
        }
        catch (CustomException ex)
        {
            return new("unknown", null, "mapping_missing", ex.Message);
        }

        return await ExecuteAsync(
                WmsOperationKind.SubmitOutboundOrder,
                "submitOutboundOrder",
                idempotencyKey,
                correlationId,
                payload,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<WmsOrderNotificationResult> CancelAsync(
        string idempotencyKey,
        string correlationId,
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required.", nameof(orderId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            operationId = orderId,
            outboundOrderId = orderId,
            reason = reason.Trim(),
        }, JsonOptions);
        return ExecuteAsync(
            WmsOperationKind.CancelOutboundOrder,
            "cancelOutboundOrder",
            idempotencyKey,
            correlationId,
            payload,
            cancellationToken);
    }

    private async Task<WmsOrderNotificationResult> ExecuteAsync(
        WmsOperationKind operationKind,
        string persistedKind,
        string idempotencyKey,
        string correlationId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            return new("unknown", null, "wms_not_configured", "The warehouse notification connection is not configured.");
        }

        string key = idempotencyKey.Trim();
        string payloadJson = payload.GetRawText();
        string requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        var operation = await FindOperationAsync(key, cancellationToken).ConfigureAwait(false);
        if (operation is not null && !string.Equals(operation.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new CustomException(
                "The idempotency key was already used for another warehouse notification.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        if (operation is not null && operation.Status is "accepted" or "completed" or "rejected")
        {
            return ToResult(operation);
        }

        if (operation is null)
        {
            operation = WmsOutboundOperation.Create(
                Guid.CreateVersion7(),
                settings.Provider,
                settings.ConnectionId,
                persistedKind,
                key,
                correlationId,
                requestHash,
                payloadJson,
                clock.GetUtcNow());
            db.OutboundOperations.Add(operation);
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                operation = await FindOperationAsync(key, cancellationToken).ConfigureAwait(false);
                if (operation is null)
                {
                    throw;
                }
                if (!string.Equals(operation.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    throw new CustomException(
                        "The idempotency key was already used for another warehouse notification.",
                        (IEnumerable<string>?)null,
                        HttpStatusCode.Conflict);
                }
            }
        }

        JsonElement outboundPayload = ParsePayload(operation.PayloadJson);
        var response = await client.ExecuteAsync(
                new WmsOperationRequest(
                    operationKind,
                    key,
                    Activity.Current?.TraceId.ToString() ?? correlationId,
                    outboundPayload),
                cancellationToken)
            .ConfigureAwait(false);
        operation.Record(response, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(operation);
    }

    private async Task<JsonElement> BuildSubmitPayloadAsync(
        WmsOutboundOrderNotice notice,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw new CustomException(
                "The warehouse notification connection is not configured.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var mappings = await db.Mappings.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Provider == settings.Provider
                && x.ConnectionId == settings.ConnectionId
                && x.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        WmsMapping Resolve(string kind, string value) => mappings.FirstOrDefault(x =>
                x.Kind == kind && string.Equals(x.FoodOsValue, value, StringComparison.OrdinalIgnoreCase))
            ?? throw new CustomException(
                $"Active WMS mapping '{kind}:{value}' is required before warehouse notification.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);

        var warehouse = Resolve(WmsMappingKinds.Warehouse, notice.WarehouseId);
        var owner = Resolve(WmsMappingKinds.Owner, notice.OwnerId);
        var store = mappings.FirstOrDefault(x => x.Kind == WmsMappingKinds.Store
            && string.Equals(x.FoodOsValue, notice.StoreId.ToString(), StringComparison.OrdinalIgnoreCase));
        var lines = notice.Lines.Select(line =>
        {
            var sku = Resolve(WmsMappingKinds.Sku, line.Sku);
            var unit = Resolve(WmsMappingKinds.Unit, line.Uom);
            if (unit.FoodOsQuantityPerExternalUnit <= 0)
            {
                throw new CustomException(
                    $"WMS unit mapping '{line.Uom}' has an invalid conversion factor.",
                    (IEnumerable<string>?)null,
                    HttpStatusCode.Conflict);
            }

            return new
            {
                lineId = line.LineId.ToString(),
                sku = line.Sku,
                externalSku = sku.ExternalValue,
                uom = line.Uom,
                externalUom = unit.ExternalValue,
                quantity = line.Quantity / unit.FoodOsQuantityPerExternalUnit,
                lotNumber = (string?)null,
                expiryDate = (DateOnly?)null,
            };
        }).ToList();

        return JsonSerializer.SerializeToElement(new
        {
            outboundOrderId = notice.OrderId,
            orderNumber = notice.OrderNumber,
            revision = notice.Revision,
            warehouseId = notice.WarehouseId,
            externalWarehouseId = warehouse.ExternalValue,
            ownerId = notice.OwnerId,
            externalOwnerId = owner.ExternalValue,
            shipBy = notice.ShipBy,
            consignee = new
            {
                storeId = notice.StoreId,
                externalStoreId = store?.ExternalValue,
                name = notice.StoreName,
                address = notice.StoreAddress,
            },
            lines,
        }, JsonOptions);
    }

    private Task<WmsOutboundOperation?> FindOperationAsync(string key, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        string provider = settings.Provider.Trim().ToUpperInvariant();
        string connection = settings.ConnectionId.Trim().ToUpperInvariant();
        return db.OutboundOperations.FirstOrDefaultAsync(x =>
            x.Provider == provider && x.ConnectionId == connection && x.IdempotencyKey == key,
            cancellationToken);
    }

    private static JsonElement ParsePayload(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static WmsOrderNotificationResult ToResult(WmsOutboundOperation operation) => new(
        operation.Status,
        operation.ExternalOperationId,
        operation.ErrorCode,
        operation.Detail);
}
