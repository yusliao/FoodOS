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

    public static class Orders
    {
        public const string Resource = "Ordering.Orders";
        public const string View = $"Permissions.{Resource}.View";
        public const string Manage = $"Permissions.{Resource}.Manage";
        public const string Reconcile = $"Permissions.{Resource}.Reconcile";
    }

    public static class StoreAccess
    {
        public const string Resource = "Ordering.StoreAccess";
        public const string View = $"Permissions.{Resource}.View";
        public const string Manage = $"Permissions.{Resource}.Manage";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Shop", ActionConstants.View, Shop.Resource, IsBasic: true, IsCustomer: true),
        new("Place Shop Orders", "Order", Shop.Resource, IsBasic: true, IsCustomer: true),
        new("View Customers", ActionConstants.View, Customers.Resource, IsBasic: true),
        new("Create Customers", ActionConstants.Create, Customers.Resource),
        new("Update Customers", ActionConstants.Update, Customers.Resource),
        new("View Stores", ActionConstants.View, Stores.Resource, IsBasic: true),
        new("Create Stores", ActionConstants.Create, Stores.Resource),
        new("Update Stores", ActionConstants.Update, Stores.Resource),
        new("View Own Store Access", ActionConstants.View, StoreAccess.Resource, IsBasic: true, IsCustomer: true),
        new("Manage Customer Store Access", "Manage", StoreAccess.Resource, IsCustomer: true),
        new("View Orders", ActionConstants.View, Orders.Resource),
        new("Manage Orders", "Manage", Orders.Resource),
        new("Reconcile Orders", "Reconcile", Orders.Resource),
    ];
}
