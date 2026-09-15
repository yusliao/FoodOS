using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Logistics.Contracts.Authorization;

public static class LogisticsPermissions
{
    public static class Vehicles
    {
        public const string Resource = "Logistics.Vehicles";
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
    }

    public static class Drivers
    {
        public const string Resource = "Logistics.Drivers";
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
    }

    public static class Routes
    {
        public const string Resource = "Logistics.Routes";
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
    }

    public static class Shipments
    {
        public const string Resource = "Logistics.Shipments";
        public const string View = $"Permissions.{Resource}.View";
        public const string ViewAssigned = $"Permissions.{Resource}.ViewAssigned";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Load = $"Permissions.{Resource}.Load";
        public const string Depart = $"Permissions.{Resource}.Depart";
    }

    public static class ProofOfDelivery
    {
        public const string Resource = "Logistics.POD";
        public const string Confirm = $"Permissions.{Resource}.Confirm";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Vehicles", ActionConstants.View, Vehicles.Resource, IsBasic: true),
        new("Create Vehicles", ActionConstants.Create, Vehicles.Resource),
        new("View Drivers", ActionConstants.View, Drivers.Resource, IsBasic: true),
        new("Create Drivers", ActionConstants.Create, Drivers.Resource),
        new("View Routes", ActionConstants.View, Routes.Resource, IsBasic: true),
        new("Create Routes", ActionConstants.Create, Routes.Resource),
        new("View Shipments", ActionConstants.View, Shipments.Resource, IsBasic: true),
        new("View Assigned Shipments", "ViewAssigned", Shipments.Resource),
        new("Create Shipments", ActionConstants.Create, Shipments.Resource),
        new("Load Shipments", "Load", Shipments.Resource),
        new("Depart Shipments", "Depart", Shipments.Resource),
        new("Confirm Proof of Delivery", "Confirm", ProofOfDelivery.Resource),
    ];
}
