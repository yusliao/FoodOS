using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Warehouse.Contracts.Authorization;

public static class WarehousePermissions
{
    public static class Locations
    {
        public const string Resource = "Warehouse.Locations";
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
    }

    public static class Waves
    {
        public const string Resource = "Warehouse.Waves";
        public const string View = $"Permissions.{Resource}.View";
        public const string Cutoff = $"Permissions.{Resource}.Cutoff";
        public const string Generate = $"Permissions.{Resource}.Generate";
        public const string Release = $"Permissions.{Resource}.Release";
    }

    public static class Picks
    {
        public const string Resource = "Warehouse.Picks";
        public const string View = $"Permissions.{Resource}.View";
        public const string Confirm = $"Permissions.{Resource}.Confirm";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Locations", ActionConstants.View, Locations.Resource, IsBasic: true),
        new("Create Locations", ActionConstants.Create, Locations.Resource),
        new("View Waves", ActionConstants.View, Waves.Resource, IsBasic: true),
        new("Trigger Cutoff", "Cutoff", Waves.Resource),
        new("Generate Waves", ActionConstants.Generate, Waves.Resource),
        new("Release Waves", "Release", Waves.Resource),
        new("View Pick Tasks", ActionConstants.View, Picks.Resource, IsBasic: true),
        new("Confirm Picks", "Confirm", Picks.Resource),
    ];
}
