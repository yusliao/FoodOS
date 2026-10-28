namespace FSH.Modules.Ordering.Contracts.Authorization;

/// <summary>Operator positions contributed by the Ordering module.</summary>
public static class OrderingRoleTemplates
{
    public const string CustomerServiceRole = "CustomerService";

    public const string CustomerServiceDescription =
        "Customer service — takes restaurant orders by phone and amends or cancels them before cutoff.";

    public static IReadOnlyList<string> CustomerServicePermissions { get; } =
    [
        OrderingPermissions.OrderDesk.View,
        OrderingPermissions.OrderDesk.Place,
        OrderingPermissions.Orders.View,
        OrderingPermissions.Orders.Manage,
        OrderingPermissions.Customers.View,
        OrderingPermissions.Stores.View,
    ];
}
