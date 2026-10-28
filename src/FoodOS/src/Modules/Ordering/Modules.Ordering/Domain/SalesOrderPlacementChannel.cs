namespace FSH.Modules.Ordering.Domain;

/// <summary>Where a platform-committed order came from (stored as text). Orders placed before this was recorded have none.</summary>
public enum SalesOrderPlacementChannel
{
    Shop = 0,
    OrderDesk = 1,
}
