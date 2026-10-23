using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
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
            // The event is already accepted in the WMS inbox, so a line that does not belong to this
            // order must not throw: that would answer 400 to every retry of an accepted event.
            var orderLineIds = order.Lines.Select(line => line.Id).ToHashSet();
            int unmatched = feedback.UnresolvedLineCount;
            foreach (var line in feedback.Lines)
            {
                if (orderLineIds.Contains(line.OrderLineId))
                {
                    order.RecordLineShortage(line.OrderLineId, line.Quantity, reason);
                }
                else
                {
                    unmatched++;
                }
            }

            string detail = unmatched == 0
                ? $"Warehouse reported shortage: {reason}."
                : $"Warehouse reported shortage: {reason}; {unmatched} line(s) did not match this order.";
            order.MarkWarehouseException(detail, feedback.OccurredAt);
        }
        else if (order.WarehouseConfirmationStatus != WarehouseConfirmationStatus.Exception)
        {
            // Later progress events must not clear an exception that still needs operator follow-up.
            // An amendment or cancellation resets the order to Pending through the normal flow.
            order.MarkWarehouseConfirmed($"Warehouse reported {feedback.EventType}.", feedback.OccurredAt);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
