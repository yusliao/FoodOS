namespace FSH.Modules.Inventory.Domain;

public enum InventoryBucket
{
    OnHand = 0,
    Held = 1,
    Allocated = 2,
    Picked = 3,
    InTransit = 4,
    Isolated = 5
}
