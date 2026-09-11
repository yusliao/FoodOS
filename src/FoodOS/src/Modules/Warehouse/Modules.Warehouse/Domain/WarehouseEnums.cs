namespace FSH.Modules.Warehouse.Domain;

public enum LocationType
{
    Storage = 0,
    Pick = 1,
    Dock = 2,
    Quarantine = 3
}

public enum WaveStatus
{
    Draft = 0,
    Released = 1,
    Picking = 2,
    Completed = 3
}

public enum PickTaskStatus
{
    Pending = 0,
    Picked = 1,
    Shorted = 2
}
