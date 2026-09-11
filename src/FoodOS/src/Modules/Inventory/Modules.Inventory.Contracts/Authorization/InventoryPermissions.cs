using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Inventory.Contracts.Authorization;

public static class InventoryPermissions
{
    public static class Warehouses
    {
        public const string Resource = "Inventory.Warehouses";
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Update = $"Permissions.{Resource}.Update";
    }

    public static class Stock
    {
        public const string Resource = "Inventory.Stock";
        public const string View = $"Permissions.{Resource}.View";
        public const string Receive = $"Permissions.{Resource}.Receive";
        public const string Adjust = $"Permissions.{Resource}.Adjust";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Warehouses", ActionConstants.View, Warehouses.Resource, IsBasic: true),
        new("Create Warehouses", ActionConstants.Create, Warehouses.Resource),
        new("Update Warehouses", ActionConstants.Update, Warehouses.Resource),
        new("View Stock", ActionConstants.View, Stock.Resource, IsBasic: true),
        new("Receive Stock", ActionConstants.Receive, Stock.Resource),
        new("Adjust Stock", ActionConstants.Adjust, Stock.Resource),
    ];
}
