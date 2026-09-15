using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Logistics.Contracts.Authorization;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Features.v1.Drivers.CreateDriver;
using FSH.Modules.Logistics.Features.v1.Drivers.SearchDrivers;
using FSH.Modules.Logistics.Features.v1.Pods.ConfirmPod;
using FSH.Modules.Logistics.Features.v1.Routes.CreateRoute;
using FSH.Modules.Logistics.Features.v1.Routes.GetRouteById;
using FSH.Modules.Logistics.Features.v1.Routes.SearchRoutes;
using FSH.Modules.Logistics.Features.v1.Shipments.CreateShipment;
using FSH.Modules.Logistics.Features.v1.Shipments.DepartShipment;
using FSH.Modules.Logistics.Features.v1.Shipments.GetMyShipments;
using FSH.Modules.Logistics.Features.v1.Shipments.GetShipmentById;
using FSH.Modules.Logistics.Features.v1.Shipments.SearchShipments;
using FSH.Modules.Logistics.Features.v1.Shipments.LoadShipment;
using FSH.Modules.Logistics.Features.v1.Vehicles.CreateVehicle;
using FSH.Modules.Logistics.Features.v1.Vehicles.SearchVehicles;
using FSH.Modules.Logistics.Jobs;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Logistics.LogisticsModule), 690)]

namespace FSH.Modules.Logistics;

public sealed class LogisticsModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(LogisticsPermissions.All);

        builder.Services.AddHeroDbContext<LogisticsDbContext>();
        builder.Services.AddScoped<IDbInitializer, LogisticsDbInitializer>();
        builder.Services.AddTransient<DispatchReminderJob>();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<LogisticsDbContext>(
                name: "db:logistics",
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
            .MapGroup("api/v{version:apiVersion}/logistics")
            .WithTags("Logistics")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapSearchVehiclesEndpoint();
        group.MapCreateVehicleEndpoint();
        group.MapSearchDriversEndpoint();
        group.MapCreateDriverEndpoint();
        group.MapSearchRoutesEndpoint();
        group.MapCreateRouteEndpoint();
        group.MapGetRouteByIdEndpoint();
        group.MapCreateShipmentEndpoint();
        group.MapSearchShipmentsEndpoint();
        group.MapGetMyShipmentsEndpoint();
        group.MapGetMyShipmentByIdEndpoint();
        group.MapGetShipmentByIdEndpoint();
        group.MapConfirmLoadShipmentEndpoint();
        group.MapConfirmDepartShipmentEndpoint();
        group.MapConfirmPodEndpoint();

        var jobManager = endpoints.ServiceProvider.GetService<IRecurringJobManager>();
        if (jobManager is not null)
        {
            jobManager.AddOrUpdate(
                "logistics-dispatch-reminder",
                Job.FromExpression<DispatchReminderJob>(j => j.RunAsync(CancellationToken.None)),
                "* * * * *",
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }
}
