using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Ops.Contracts.Authorization;

public static class OpsPermissions
{
    public static class Kpis
    {
        public const string Resource = "Ops.Kpis";
        public const string View = $"Permissions.{Resource}.View";
    }

    public static class Trace
    {
        public const string Resource = "Ops.Trace";
        public const string View = $"Permissions.{Resource}.View";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View operations KPIs", ActionConstants.View, Kpis.Resource, IsBasic: true),
        new("View lot trace timeline", ActionConstants.View, Trace.Resource, IsBasic: true),
    ];
}
