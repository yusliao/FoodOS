using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Ordering.Contracts.Authorization;

public static class OrderingPermissions
{
    public static class Shop
    {
        public const string Resource = "Ordering.Shop";
        public const string View = $"Permissions.{Resource}.View";
        public const string Order = $"Permissions.{Resource}.Order";
    }

    public static class Customers
    {
        public const string Resource = "Ordering.Customers";
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Update = $"Permissions.{Resource}.Update";
    }

    public static class Stores
    {
        public const string Resource = "Ordering.Stores";
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Update = $"Permissions.{Resource}.Update";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Shop", ActionConstants.View, Shop.Resource, IsBasic: true),
        new("Place Shop Orders", "Order", Shop.Resource, IsBasic: true),
        new("View Customers", ActionConstants.View, Customers.Resource, IsBasic: true),
        new("Create Customers", ActionConstants.Create, Customers.Resource),
        new("Update Customers", ActionConstants.Update, Customers.Resource),
        new("View Stores", ActionConstants.View, Stores.Resource, IsBasic: true),
        new("Create Stores", ActionConstants.Create, Stores.Resource),
        new("Update Stores", ActionConstants.Update, Stores.Resource),
    ];
}
