namespace FSH.Modules.Inventory.Contracts.Dtos;

public sealed record OperatingClockDto(
    string CutoffLocal,
    string LoadLocal,
    string DeliverFromLocal,
    string DeliverToLocal,
    string ReconcileLocal,
    string TimeZoneId);
