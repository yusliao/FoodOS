using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Inventory.Contracts.v1.Plans;
using FSH.Modules.Inventory.Contracts.v1.Stock;
using FSH.Modules.Logistics.Contracts.v1.ProofOfDelivery;
using FSH.Modules.Logistics.Contracts.v1.Shipments;
using FSH.Modules.Ordering.Contracts.v1.Orders;
using FSH.Modules.Ordering.Contracts.v1.Shop;
using FSH.Modules.Procurement.Contracts.v1.QualityChecks;
using FSH.Modules.Warehouse.Contracts.v1.Cutoff;
using FSH.Modules.Warehouse.Contracts.v1.Locations;
using FSH.Modules.Warehouse.Contracts.v1.Pack;
using FSH.Modules.Warehouse.Contracts.v1.Picks;
using FSH.Modules.Warehouse.Contracts.v1.Putaway;
using FSH.Modules.Warehouse.Contracts.v1.Shrinkage;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using Mediator;

namespace FoodOS.Api;

/// <summary>
/// Fail closed while replacing the legacy execution chain with confirmed external WMS contracts.
/// This host supports external WMS only; there is deliberately no runtime local-execution switch.
/// Query/projection ingestion is separate from these legacy physical execution commands.
/// </summary>
internal static class ExternalWmsExecutionBoundary
{
    private static readonly HashSet<Type> LocalExecutionCommands =
    [
        typeof(ReceiveInventoryCommand), typeof(ReceiveIsolatedStockCommand),
        typeof(ReserveStockCommand), typeof(UnreserveStockCommand), typeof(AllocateReservationCommand),
        typeof(PickAllocatedStockCommand), typeof(ShipPickedStockCommand), typeof(DeliverInTransitStockCommand),
        typeof(ReturnInTransitStockCommand), typeof(IsolateStockCommand),
        typeof(AdjustCountStockCommand), typeof(AdjustShrinkStockCommand), typeof(CreateDailyPlanCommand),
        typeof(PassQualityCheckCommand), typeof(FailQualityCheckCommand),
        typeof(CreateLocationCommand), typeof(CreatePutawayTaskCommand), typeof(ConfirmPutawayCommand),
        typeof(ConfirmCutoffCommand), typeof(GenerateWaveCommand), typeof(AssignWaveCommand),
        typeof(StartWaveCommand), typeof(ConfirmPickTaskCommand), typeof(CreatePackToteCommand), typeof(CreateShrinkageCommand),
        // Legacy operator placement still invokes local inventory. Shop placement and order changes
        // are platform commitments and only enqueue asynchronous WMS notifications.
        typeof(PlaceOrderCommand),
        typeof(LockOrdersForCutoffCommand), typeof(StartOrderPickingCommand),
        typeof(ConfirmOrderPackedCommand), typeof(RecordOrderLineShortageCommand),
        typeof(StartOrderInTransitCommand), typeof(ConfirmOrderReceivedCommand),
        typeof(CreateShipmentCommand), typeof(LoadShipmentCommand), typeof(DepartShipmentCommand), typeof(ConfirmPodCommand),
    ];

    public static bool Blocks(Type messageType) => LocalExecutionCommands.Contains(messageType);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Instantiated by DI as an open generic Mediator pipeline behavior.")]
internal sealed class ExternalWmsExecutionBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public ExternalWmsExecutionBehavior() { }
    public ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (ExternalWmsExecutionBoundary.Blocks(typeof(TMessage)))
            throw new CustomException(
                "External WMS confirmation is required. Local warehouse execution is disabled; this operation was not executed.",
                (IEnumerable<string>?)null, HttpStatusCode.Conflict);
        return next(message, cancellationToken);
    }
}
