using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateInboundAppointment;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreatePurchaseOrder;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.GetPurchaseOrderById;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.SearchPurchaseOrders;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.SendPurchaseOrder;
using FSH.Modules.Procurement.Features.v1.QualityChecks.FailQualityCheck;
using FSH.Modules.Procurement.Features.v1.QualityChecks.PassQualityCheck;
using FSH.Modules.Procurement.Features.v1.Suppliers.CreateSupplier;
using FSH.Modules.Procurement.Features.v1.Suppliers.GetSupplierById;
using FSH.Modules.Procurement.Features.v1.Suppliers.SearchSuppliers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Procurement.ProcurementModule), 670)]

namespace FSH.Modules.Procurement;

public sealed class ProcurementModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(ProcurementPermissions.All);

        builder.Services.AddHeroDbContext<ProcurementDbContext>();
        builder.Services.AddScoped<IDbInitializer, ProcurementDbInitializer>();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<ProcurementDbContext>(
                name: "db:procurement",
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
            .MapGroup("api/v{version:apiVersion}/procurement")
            .WithTags("Procurement")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapSearchSuppliersEndpoint();
        group.MapCreateSupplierEndpoint();
        group.MapGetSupplierByIdEndpoint();
        group.MapSearchPurchaseOrdersEndpoint();
        group.MapCreatePurchaseOrderEndpoint();
        group.MapGetPurchaseOrderByIdEndpoint();
        group.MapSendPurchaseOrderEndpoint();
        group.MapCreateInboundAppointmentEndpoint();
        group.MapConfirmPassQualityCheckEndpoint();
        group.MapConfirmFailQualityCheckEndpoint();
    }
}
