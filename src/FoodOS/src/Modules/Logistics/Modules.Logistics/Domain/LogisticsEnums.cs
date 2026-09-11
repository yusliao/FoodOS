namespace FSH.Modules.Logistics.Domain;

public enum ShipmentStatus
{
    Created = 0,
    Loading = 1,
    Departed = 2,
    Completed = 3
}

public enum StopStatus
{
    Pending = 0,
    Delivered = 1
}
