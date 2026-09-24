using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using FSH.Modules.WmsIntegration.Contracts.v1;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Services;

public sealed class WarehouseOrderFeedbackSink(OrderingDbContext db) : IWarehouseOrderFeedbackSink
{
    public async Task ApplyAsync(
        WarehouseOrderFeedback feedback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        var order = await db.SalesOrders
            .SingleOrDefaultAsync(item => item.Id == feedback.OrderId, cancellationToken)
            .ConfigureAwait(false);
        if (order is null)
        {
            return;
        }

        if (feedback.EventType == WmsEventTypes.OutboundShortage)
        {
            string reason = string.IsNullOrWhiteSpace(feedback.ReasonCode)
                ? "warehouse_shortage"
                : feedback.ReasonCode.Trim();
            foreach (var line in feedback.Lines)
            {
                order.RecordLineShortage(line.OrderLineId, line.Quantity, reason);
            }

            order.MarkWarehouseException($"Warehouse reported shortage: {reason}.", feedback.OccurredAt);
        }
        else
        {
            order.MarkWarehouseConfirmed($"Warehouse reported {feedback.EventType}.", feedback.OccurredAt);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
