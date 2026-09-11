using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Procurement.Contracts.Authorization;

public static class ProcurementPermissions
{
    public static class Suppliers
    {
        public const string Resource = "Procurement.Suppliers";
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Update = $"Permissions.{Resource}.Update";
    }

    public static class Purchase
    {
        public const string Resource = "Procurement.Purchase";
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
    }

    public static class Quality
    {
        public const string Resource = "Procurement.Quality";
        public const string View = $"Permissions.{Resource}.View";
        public const string Pass = $"Permissions.{Resource}.Pass";
        public const string Fail = $"Permissions.{Resource}.Fail";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Suppliers", ActionConstants.View, Suppliers.Resource, IsBasic: true),
        new("Create Suppliers", ActionConstants.Create, Suppliers.Resource),
        new("Update Suppliers", ActionConstants.Update, Suppliers.Resource),
        new("View Purchase Orders", ActionConstants.View, Purchase.Resource, IsBasic: true),
        new("Create Purchase Orders", ActionConstants.Create, Purchase.Resource),
        new("View Quality Checks", ActionConstants.View, Quality.Resource, IsBasic: true),
        new("Pass Quality Check", "Pass", Quality.Resource),
        new("Fail Quality Check", "Fail", Quality.Resource),
    ];
}
