namespace FSH.Modules.WmsIntegration.Contracts.v1;

public static class WmsEventTypes
{
    public const string InventorySnapshot = "inventory.snapshot";
    public const string InventoryChanged = "inventory.changed";
    public const string InventoryAdjusted = "inventory.adjusted";
    public const string InboundReceived = "inbound.received";
    public const string InboundQualityCompleted = "inbound.qualityCompleted";
    public const string InboundPutawayCompleted = "inbound.putawayCompleted";
    public const string OutboundAllocated = "outbound.allocated";
    public const string OutboundPicked = "outbound.picked";
    public const string OutboundShortage = "outbound.shortage";
    public const string OutboundLoaded = "outbound.loaded";
    public const string OutboundShipped = "outbound.shipped";
    public const string ReturnReceived = "return.received";
}

public static class WmsEntityTypes
{
    public const string Inventory = "inventory";
    public const string InboundOrder = "inboundOrder";
    public const string OutboundOrder = "outboundOrder";
    public const string Return = "return";
}
