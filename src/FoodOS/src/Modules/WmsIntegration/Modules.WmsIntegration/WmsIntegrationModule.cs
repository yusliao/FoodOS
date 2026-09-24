using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.HttpResilience;
using FSH.Framework.Web.Modules;
using FSH.Modules.WmsIntegration.Contracts.Authorization;
using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.WmsIntegration.Data;
using FSH.Modules.WmsIntegration.Features.v1.GetStatus;
using FSH.Modules.WmsIntegration.Features.v1.Mappings.SearchMappings;
using FSH.Modules.WmsIntegration.Features.v1.Mappings.UpsertMapping;
using FSH.Modules.WmsIntegration.Features.v1.Mappings.ValidateMappings;
using FSH.Modules.WmsIntegration.Features.v1.ReceiveEvent;
using FSH.Modules.WmsIntegration.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

[assembly: FshModule(typeof(FSH.Modules.WmsIntegration.WmsIntegrationModule), 675)]

namespace FSH.Modules.WmsIntegration;

public sealed class WmsIntegrationModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        PermissionConstants.Register(WmsIntegrationPermissions.All);

        builder.Services.AddOptions<WmsIntegrationOptions>()
            .Bind(builder.Configuration.GetSection(WmsIntegrationOptions.SectionName));
        builder.Services.AddHeroDbContext<WmsIntegrationDbContext>();
        builder.Services.AddScoped<IDbInitializer, WmsIntegrationDbInitializer>();
        builder.Services.AddScoped<WmsInboxService>();
        builder.Services.AddSingleton<IWmsReadiness, WmsReadiness>();
        builder.Services.AddHttpClient<IWmsStandardClient, WmsStandardClient>((services, client) =>
        {
            var options = services.GetRequiredService<IOptions<WmsIntegrationOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress)) client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.RequestTimeoutSeconds, 1, 120));
        }).AddHeroResilience(builder.Configuration);

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<WmsIntegrationDbContext>(
                name: "db:wms-integration",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void ConfigureMiddleware(IApplicationBuilder app) { }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        endpoints.MapGroup("api/v{version:apiVersion}/wms/inbound")
            .WithTags("WMS Integration")
            .WithApiVersionSet(versionSet)
            .MapWmsEventEndpoint();

        var management = endpoints.MapGroup("api/v{version:apiVersion}/wms")
            .WithTags("WMS Integration")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();
        management.MapWmsStatusEndpoint();
        management.MapUpsertWmsMappingEndpoint();
        management.MapSearchWmsMappingsEndpoint();
        management.MapResolveWmsMappingsEndpoint();
    }
}
