using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Data;
using FSH.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Inventory.Features.v1.Stock;

internal static class ReceiptReplay
{
    public static async Task<Guid?> FindAsync(
        InventoryDbContext dbContext, string key, Guid warehouseId, Guid zoneId, Guid productId,
        string lotNo, decimal quantity, DateOnly expiryDate, DateOnly? manufacturedOn,
        string refType, CancellationToken cancellationToken)
    {
        var receipt = await dbContext.InventoryTransactions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdempotencyKey == key, cancellationToken).ConfigureAwait(false);
        if (receipt is null) return null;
        var lot = await dbContext.Lots.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == receipt.LotId, cancellationToken).ConfigureAwait(false);
        if (receipt.Type != InventoryTransactionType.Receive || receipt.RefType != refType
            || receipt.WarehouseId != warehouseId || receipt.ZoneId != zoneId
            || receipt.ProductId != productId || receipt.Quantity != quantity || lot is null
            || lot.ProductId != productId || !string.Equals(lot.LotNo, lotNo.Trim(), StringComparison.OrdinalIgnoreCase)
            || lot.ExpiryDate != expiryDate || lot.ManufacturedOn != manufacturedOn)
        {
            throw new CustomException(
                "This receipt key is already associated with different stock or lot details.",
                (IEnumerable<string>?)null, HttpStatusCode.Conflict);
        }
        return lot.Id;
    }
}
