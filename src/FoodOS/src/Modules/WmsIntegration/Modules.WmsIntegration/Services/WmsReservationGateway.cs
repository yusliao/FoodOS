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

public sealed class WmsReservationGateway(
    WmsIntegrationDbContext db,
    IWmsStandardClient client,
    IOptions<WmsIntegrationOptions> options,
    TimeProvider clock) : IWmsReservationGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WmsReservationResult?> FindAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var operation = await FindOperationAsync(idempotencyKey.Trim(), cancellationToken).ConfigureAwait(false);
        return operation is null ? null : ToResult(operation);
    }

    public async Task<WmsReservationResult> ReserveAsync(
        string idempotencyKey,
        string correlationId,
        WmsReserveOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0))
        {
            throw new CustomException(
                "A WMS reservation requires positive order lines.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            return new(Guid.Empty, "unknown", null, "wms_not_configured", "The WMS connection is not configured.");
        }

        string key = idempotencyKey.Trim();
        string logicalJson = JsonSerializer.Serialize(request, JsonOptions);
        string requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logicalJson)));
        var operation = await FindOperationAsync(key, cancellationToken).ConfigureAwait(false);
        if (operation is not null && !string.Equals(operation.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new CustomException(
                "The idempotency key was already used for a different WMS reservation request.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        bool queryExisting = operation is not null && operation.Status is "accepted" or "unknown";
        if (operation is not null && operation.Status is "completed" or "rejected")
        {
            return ToResult(operation);
        }

        if (operation is null)
        {
            Guid operationId = Guid.CreateVersion7();
            JsonElement payload = await BuildPayloadAsync(operationId, request, cancellationToken).ConfigureAwait(false);
            operation = WmsOutboundOperation.Create(
                operationId,
                settings.Provider,
                settings.ConnectionId,
                "reserve",
                key,
                correlationId,
                requestHash,
                payload.GetRawText(),
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
                        "The idempotency key was already used for a different WMS reservation request.",
                        (IEnumerable<string>?)null,
                        HttpStatusCode.Conflict);
                }
                queryExisting = operation.Status is "accepted" or "unknown";
            }
        }

        JsonElement outboundPayload = queryExisting
            ? JsonSerializer.SerializeToElement(new { idempotencyKey = key }, JsonOptions)
            : ParsePayload(operation.PayloadJson);
        var response = await client.ExecuteAsync(
            new WmsOperationRequest(
                queryExisting ? WmsOperationKind.Query : WmsOperationKind.Reserve,
                key,
                Activity.Current?.TraceId.ToString() ?? correlationId,
                outboundPayload),
            cancellationToken).ConfigureAwait(false);

        if (string.Equals(response.Status, "completed", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(response.ExternalOperationId))
        {
            response = response with
            {
                Status = "unknown",
                ErrorCode = "missing_reservation_id",
                Detail = "WMS completed the reservation without a reservation identifier.",
            };
        }

        operation.Record(response, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(operation);
    }

    private async Task<JsonElement> BuildPayloadAsync(
        Guid operationId,
        WmsReserveOrderRequest request,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var mappings = await db.Mappings.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Provider == settings.Provider
                && x.ConnectionId == settings.ConnectionId
                && x.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        WmsMapping Resolve(string kind, string value) => mappings.FirstOrDefault(x =>
                x.Kind == kind && string.Equals(x.FoodOsValue, value, StringComparison.OrdinalIgnoreCase))
            ?? throw new CustomException(
                $"Active WMS mapping '{kind}:{value}' is required before reservation.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);

        var warehouse = Resolve(WmsMappingKinds.Warehouse, request.WarehouseId);
        var owner = Resolve(WmsMappingKinds.Owner, request.OwnerId);
        var lines = request.Lines.Select(line =>
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
                lineId = line.LineId,
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
            operationId,
            orderId = operationId,
            warehouseId = request.WarehouseId,
            externalWarehouseId = warehouse.ExternalValue,
            ownerId = request.OwnerId,
            externalOwnerId = owner.ExternalValue,
            expiresAt = (DateTimeOffset?)null,
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

    private static WmsReservationResult ToResult(WmsOutboundOperation operation) => new(
        operation.Id,
        operation.Status,
        operation.ExternalOperationId,
        operation.ErrorCode,
        operation.Detail);
}
