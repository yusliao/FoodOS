using FSH.Framework.Web;
using FSH.Framework.Web.Modules;
using FSH.Modules.Auditing;
using FSH.Modules.Identity;
using FSH.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using FSH.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using FSH.Modules.Multitenancy;
using FSH.Modules.Multitenancy.Contracts.v1.GetTenantStatus;
using FSH.Modules.Webhooks;
using FSH.Modules.Billing;
using FSH.Modules.Catalog;
using FSH.Modules.Inventory;
using FSH.Modules.Ordering;
using FSH.Modules.Procurement;
using FSH.Modules.Warehouse;
using FSH.Modules.Logistics;
using FSH.Modules.Ops;
using FSH.Modules.WmsIntegration;
using FSH.Modules.WmsIntegration.Contracts.v1;
using FSH.Modules.Tickets;
using FSH.Modules.Multitenancy.Features.v1.GetTenantStatus;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Serialize enums as string names (reads still accept names or integers). [Flags] enums (AuditTag, BodyCapture)
// opt back to numeric via their own NumericEnumConverter since comma-joined flag strings break bitwise consumers. Frontends mirror this as string unions.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

if (builder.Environment.IsProduction())
{
    static void Require(IConfiguration config, string key)
    {
        if (string.IsNullOrWhiteSpace(config[key]))
        {
            throw new InvalidOperationException($"Missing required configuration '{key}' in Production.");
        }
    }

    var config = builder.Configuration;
    Require(config, "DatabaseOptions:ConnectionString");
    Require(config, "CachingOptions:Redis");
    Require(config, "JwtOptions:SigningKey");
}

builder.Services.AddMediator(o =>
{
    o.ServiceLifetime = ServiceLifetime.Scoped;
    o.Assemblies = [
        typeof(GenerateTokenCommand),
        typeof(GenerateTokenCommandHandler),
        typeof(GetTenantStatusQuery),
        typeof(GetTenantStatusQueryHandler),
        typeof(FSH.Modules.Auditing.Contracts.AuditEnvelope),
        typeof(FSH.Modules.Auditing.Persistence.AuditDbContext),
        typeof(FSH.Modules.Webhooks.Contracts.v1.CreateWebhookSubscription.CreateWebhookSubscriptionCommand),
        typeof(FSH.Modules.Webhooks.WebhooksModule),
        typeof(FSH.Modules.Billing.Contracts.BillingContractsMarker),
        typeof(FSH.Modules.Billing.BillingModule),
        typeof(FSH.Modules.Catalog.Contracts.CatalogContractsMarker),
        typeof(FSH.Modules.Catalog.CatalogModule),
        typeof(FSH.Modules.Inventory.Contracts.InventoryContractsMarker),
        typeof(FSH.Modules.Inventory.InventoryModule),
        typeof(FSH.Modules.Ordering.Contracts.OrderingContractsMarker),
        typeof(FSH.Modules.Ordering.OrderingModule),
        typeof(FSH.Modules.Procurement.Contracts.ProcurementContractsMarker),
        typeof(FSH.Modules.Procurement.ProcurementModule),
        typeof(FSH.Modules.Warehouse.Contracts.WarehouseContractsMarker),
        typeof(FSH.Modules.Warehouse.WarehouseModule),
        typeof(FSH.Modules.Logistics.Contracts.LogisticsContractsMarker),
        typeof(FSH.Modules.Logistics.LogisticsModule),
        typeof(FSH.Modules.Ops.Contracts.OpsContractsMarker),
        typeof(FSH.Modules.Ops.OpsModule),
        typeof(FSH.Modules.WmsIntegration.Contracts.WmsIntegrationContractsMarker),
        typeof(FSH.Modules.WmsIntegration.WmsIntegrationModule),
        typeof(FSH.Modules.Tickets.Contracts.TicketsContractsMarker),
        typeof(FSH.Modules.Tickets.TicketsModule),
        typeof(FSH.Modules.Files.Contracts.v1.Commands.RequestUploadUrlCommand),
        typeof(FSH.Modules.Files.FilesModule),
        typeof(FSH.Modules.Chat.Contracts.v1.Commands.CreateChannelCommand),
        typeof(FSH.Modules.Chat.ChatModule),
        typeof(FSH.Modules.Notifications.Contracts.v1.Commands.MarkNotificationReadCommand),
        typeof(FSH.Modules.Notifications.NotificationsModule)];
});

var moduleAssemblies = new Assembly[]
{
    typeof(IdentityModule).Assembly,
    typeof(MultitenancyModule).Assembly,
    typeof(AuditingModule).Assembly,
    typeof(FSH.Modules.Files.FilesModule).Assembly,
    typeof(WebhooksModule).Assembly,
    typeof(BillingModule).Assembly,
    typeof(CatalogModule).Assembly,
    typeof(InventoryModule).Assembly,
    typeof(OrderingModule).Assembly,
    typeof(ProcurementModule).Assembly,
    typeof(WarehouseModule).Assembly,
    typeof(LogisticsModule).Assembly,
    typeof(OpsModule).Assembly,
    typeof(WmsIntegrationModule).Assembly,
    typeof(TicketsModule).Assembly,
    typeof(FSH.Modules.Chat.ChatModule).Assembly,
    typeof(FSH.Modules.Notifications.NotificationsModule).Assembly,
};

builder.Services.AddScoped(typeof(Mediator.IPipelineBehavior<,>), typeof(FoodOS.Api.ExternalWmsExecutionBehavior<,>));
builder.AddHeroPlatform(o =>
{
    o.EnableCaching = true;
    o.EnableMailing = true;
    o.EnableJobs = true;
    o.EnableQuotas = true;
    o.EnableSse = true;
    o.EnableRealtime = true;
});

builder.AddModules(moduleAssemblies);

// Self-heal deployments carrying retired per-module `{module}-outbox-dispatcher` Hangfire recurring jobs
// (the outbox is now dispatched by OutboxDispatcherHostedService). No-op once the storage is clean.
builder.Services.AddHostedService<FoodOS.Api.OrphanedOutboxRecurringJobCleanupService>();

// Demo data is provisioned by the DbMigrator's `seed-demo` verb, not the API — the API never mutates data on startup.
// See src/Host/FoodOS.DbMigrator/README.md.

var app = builder.Build();

app.UseHeroMultiTenantDatabases();
app.UseHeroPlatform(p =>
{
    p.MapModules = true;
    p.ServeStaticFiles = true;
    p.UseQuotas = true;
    p.MapSseEndpoints = true;
    p.MapRealtime = true;
});

app.MapGet("/", () => Results.Ok(new { message = "hello world!" }))
   .WithTags("PlayGround")
   .AllowAnonymous();
app.MapGet("/api/v1/fulfillment/capabilities", async (
    HttpContext context,
    IWmsReadiness readiness,
    CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(await readiness.GetSnapshotAsync(cancellationToken).ConfigureAwait(false));
}).RequireAuthorization().WithName("GetFulfillmentCapabilities").WithTags("Fulfillment");
await app.RunAsync();
