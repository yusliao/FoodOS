using Asp.Versioning;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Ops.Contracts.Authorization;
using FSH.Modules.Ops.Features.v1.Kpis.GetOpsKpis;
using FSH.Modules.Ops.Features.v1.Trace.GetLotTrace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Ops.OpsModule), 695)]

namespace FSH.Modules.Ops;

public sealed class OpsModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(OpsPermissions.All);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/ops")
            .WithTags("Ops")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapGetOpsKpisEndpoint();
        group.MapGetLotTraceEndpoint();
    }
}
