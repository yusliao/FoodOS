using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Warehouse.Contracts.Authorization;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Features.v1.Cutoff.ConfirmCutoff;
using FSH.Modules.Warehouse.Features.v1.Locations.CreateLocation;
using FSH.Modules.Warehouse.Features.v1.Picks.ConfirmPickTask;
using FSH.Modules.Warehouse.Features.v1.Picks.GetMyPickTasks;
using FSH.Modules.Warehouse.Features.v1.Waves.GenerateWave;
using FSH.Modules.Warehouse.Features.v1.Waves.GetWaveById;
using FSH.Modules.Warehouse.Features.v1.Waves.SearchWaves;
using FSH.Modules.Warehouse.Features.v1.Waves.StartWave;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Warehouse.WarehouseModule), 680)]

namespace FSH.Modules.Warehouse;

public sealed class WarehouseModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(WarehousePermissions.All);

        builder.Services.AddHeroDbContext<WarehouseDbContext>();
        builder.Services.AddScoped<IDbInitializer, WarehouseDbInitializer>();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<WarehouseDbContext>(
                name: "db:warehouse",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        // No custom middleware needed
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/warehouse")
            .WithTags("Warehouse")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapConfirmCutoffEndpoint();
        group.MapCreateLocationEndpoint();
        group.MapSearchWavesEndpoint();
        group.MapGenerateWaveEndpoint();
        group.MapGetWaveByIdEndpoint();
        group.MapStartWaveEndpoint();
        group.MapGetMyPickTasksEndpoint();
        group.MapConfirmPickTaskEndpoint();
    }
}
