using FSH.Framework.Shared.Constants;

namespace FSH.Modules.WmsIntegration.Contracts.Authorization;

public static class WmsIntegrationPermissions
{
    public static class Integration
    {
        public const string Resource = "WmsIntegration";
        public const string View = $"Permissions.{Resource}.View";
        public const string ManageMappings = $"Permissions.{Resource}.ManageMappings";
        public const string Replay = $"Permissions.{Resource}.Replay";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View WMS integration", ActionConstants.View, Integration.Resource, IsBasic: true),
        new("Manage WMS mappings", "ManageMappings", Integration.Resource),
        new("Replay WMS integration messages", "Replay", Integration.Resource),
    ];
}
