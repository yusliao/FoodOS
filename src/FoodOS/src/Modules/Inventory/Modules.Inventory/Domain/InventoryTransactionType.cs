namespace FSH.Modules.Inventory.Domain;

public enum InventoryTransactionType
{
    Receive = 0,
    Isolate = 1,
    ReleaseIsolate = 2,
    Reserve = 3,
    Unreserve = 4,
    Allocate = 5,
    Unallocate = 6,
    Pick = 7,
    Unpick = 8,
    Load = 9,
    Ship = 10,
    Deliver = 11,
    ReturnToWarehouse = 12,
    AdjustShrink = 13,
    AdjustCount = 14
}
