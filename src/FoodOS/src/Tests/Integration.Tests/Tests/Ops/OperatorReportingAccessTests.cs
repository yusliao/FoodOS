using FSH.Modules.Auditing.Contracts.Authorization;
using FSH.Modules.Auditing;
using FSH.Modules.Auditing.Contracts;
using FSH.Modules.Auditing.Persistence;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Ops.Contracts.Authorization;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Ops;

[Collection(FshCollectionDefinition.Name)]
public sealed class OperatorReportingAccessTests(FshWebApplicationFactory factory)
{
    [Fact]
    public async Task ReportsAndAudits_Should_RequireTheirOwnOperatorPermissions()
    {
        // This test exercises read authorization, independently of the asynchronous audit writer.
        var audit = new AuditRecord
        {
            Id = Guid.NewGuid(), OccurredAtUtc = DateTime.UtcNow, ReceivedAtUtc = DateTime.UtcNow,
            TenantId = TestConstants.RootTenantId, EventType = (int)AuditEventType.Activity,
            Severity = (byte)AuditSeverity.Information, Source = "permission-test",
            CorrelationId = Guid.NewGuid().ToString("N"), TraceId = Guid.NewGuid().ToString("N"), PayloadJson = "{}",
        };
        using (var scope = factory.Services.CreateScope())
        {
            var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
                .GetAsync(TestConstants.RootTenantId);
            scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
                new MultiTenantContext<AppTenantInfo>(tenant);
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            db.AuditRecords.Add(audit);
            await db.SaveChangesAsync();
        }
        using var employee = await OperatorTestUsers.CreateOperatorAsync(factory);
        using var analyst = await OperatorTestUsers.CreateOperatorAsync(factory, OpsPermissions.Kpis.View);
        using var auditor = await OperatorTestUsers.CreateOperatorAsync(factory, AuditingPermissions.AuditTrails.View);
        string kpis = $"{TestConstants.OpsBasePath}/kpis";
        string trace = $"{TestConstants.OpsBasePath}/lots/{Guid.NewGuid()}/trace";
        foreach (var path in new[] { kpis, trace, TestConstants.AuditsBasePath,
            $"{TestConstants.AuditsBasePath}/{audit.Id}", $"{TestConstants.AuditsBasePath}/summary",
            $"{TestConstants.AuditsBasePath}/by-correlation/{Uri.EscapeDataString(audit.CorrelationId!)}",
            $"{TestConstants.AuditsBasePath}/by-trace/{Uri.EscapeDataString(audit.TraceId!)}" })
        {
            using var denied = await employee.GetAsync(path);
            denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
        }
        using var report = await analyst.GetAsync(kpis);
        report.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var analystAudit = await analyst.GetAsync($"{TestConstants.AuditsBasePath}/{audit.Id}");
        analystAudit.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var analystTrace = await analyst.GetAsync(trace);
        analystTrace.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var auditDetail = await auditor.GetAsync($"{TestConstants.AuditsBasePath}/{audit.Id}");
        auditDetail.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var auditorReport = await auditor.GetAsync(kpis);
        auditorReport.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
