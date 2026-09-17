using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Files.Contracts.Authorization;
using FSH.Modules.Files.Contracts.v1.DTOs;
using FSH.Modules.Files.Data;
using FSH.Modules.Files.Domain;
using FSH.Modules.Tickets.Contracts.Authorization;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Tickets;

public sealed partial class TicketTenantIsolationTests
{
    [Fact]
    public async Task OwnerAttachmentList_Should_ScopeRowsAndCountsBeforePaging_AndDenyNonParticipants()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string tenantA = $"list-a-{suffix}";
        string tenantB = $"list-b-{suffix}";
        using var customerA = await ProvisionTenantClientAsync(admin, tenantA);
        using var customerB = await ProvisionTenantClientAsync(admin, tenantB);
        using var support = await OperatorTestUsers.CreateOperatorAsync(_factory, TicketsPermissions.Tickets.View);
        using var unprivileged = await OperatorTestUsers.CreateOperatorAsync(_factory, FilesPermissions.Upload);
        string memberEmail = $"list-member-{suffix}@test.com";
        await CreateActiveBasicUserAsync(tenantA, memberEmail, $"list-member-{suffix}");
        using var member = await _auth.CreateAuthenticatedClientAsync(memberEmail, TestConstants.DefaultPassword, tenantA);
        Guid ticket = await CreateTicketAsync(customerA, $"Attachment list {suffix}");
        Guid emptyTicket = await CreateTicketAsync(customerA, $"Empty attachment list {suffix}");
        Guid first = await SeedListedAttachmentAsync(tenantA, ticket, "Ticket");
        Guid second = await SeedListedAttachmentAsync("root", ticket, "ticket");
        await SeedListedAttachmentAsync(tenantB, ticket, "Ticket");
        await SeedListedAttachmentAsync("root", ticket, "MyFiles");
        await SeedListedAttachmentAsync("root", Guid.NewGuid(), "Ticket");
        await SeedListedAttachmentAsync("root", ticket, "Ticket", visibility: Visibility.Public);
        await SeedListedAttachmentAsync(tenantA, ticket, "Ticket", pending: true);
        await SeedListedAttachmentAsync("root", ticket, "Ticket", infected: true);
        await SeedListedAttachmentAsync(tenantA, ticket, "Ticket", deleted: true);

        foreach (var reader in new[] { support, customerA })
        {
            var ids = new List<Guid>();
            for (int page = 1; page <= 2; page++)
            {
                using var response = await reader.GetAsync($"/api/v1/files/owners/Ticket/{ticket}?pageNumber={page}&pageSize=1");
                response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
                var result = await response.DeserializeAsync<PagedResponse<FileAssetDto>>();
                result.TotalCount.ShouldBe(2);
                result.TotalPages.ShouldBe(2);
                result.PageNumber.ShouldBe(page);
                result.Items.Count.ShouldBe(1);
                var file = result.Items.Single();
                file.PublicUrl.ShouldBeNull();
                file.Visibility.ShouldBe(Visibility.Private);
                ids.Add(file.Id);
            }
            ids.Distinct().Count().ShouldBe(2);
            ids.ShouldContain(first);
            ids.ShouldContain(second);
            using var beyond = await reader.GetAsync($"/api/v1/files/owners/ticket/{ticket}?pageNumber=3&pageSize=1");
            var beyondPage = await beyond.DeserializeAsync<PagedResponse<FileAssetDto>>();
            beyondPage.Items.ShouldBeEmpty();
            beyondPage.TotalCount.ShouldBe(2);
            using var empty = await reader.GetAsync($"/api/v1/files/owners/Ticket/{emptyTicket}");
            var emptyPage = await empty.DeserializeAsync<PagedResponse<FileAssetDto>>();
            emptyPage.TotalCount.ShouldBe(0);
            emptyPage.Items.ShouldBeEmpty();
        }

        foreach (var reader in new[] { customerB, member, unprivileged })
        {
            using var denied = await reader.GetAsync($"/api/v1/files/owners/Ticket/{ticket}");
            denied.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await denied.Content.ReadAsStringAsync()).ShouldNotContain(first.ToString());
        }
        using var deleteTicket = await customerA.DeleteAsync($"/api/v1/tickets/{ticket}");
        deleteTicket.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        foreach (var reader in new[] { support, customerA })
        {
            using var denied = await reader.GetAsync($"/api/v1/files/owners/Ticket/{ticket}");
            denied.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task OwnerAttachmentList_Should_RejectInvalidPagingUnknownOwnersAndAnonymousAccess()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        Guid ticket = await CreateTicketAsync(admin, "Attachment query validation");
        foreach (string query in new[] { "pageNumber=0", "pageNumber=1000001", "pageSize=0", "pageSize=101" })
        {
            using var invalid = await admin.GetAsync($"/api/v1/files/owners/Ticket/{ticket}?{query}");
            invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
        foreach (string owner in new[] { "Unknown", "MyFiles", "Product" })
        {
            using var denied = await admin.GetAsync($"/api/v1/files/owners/{owner}/{ticket}");
            denied.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
        using var missing = await admin.GetAsync($"/api/v1/files/owners/Ticket/{Guid.NewGuid()}");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var anonymous = _factory.CreateClient();
        using var unauthenticated = await anonymous.GetAsync($"/api/v1/files/owners/Ticket/{ticket}");
        unauthenticated.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> SeedListedAttachmentAsync(string tenantId, Guid ticketId, string ownerType,
        Visibility visibility = Visibility.Private, bool pending = false, bool infected = false, bool deleted = false)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync(tenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var file = FileAsset.CreatePending(Guid.NewGuid(), ownerType, ticketId, "evidence.pdf", "evidence.pdf",
            "application/pdf", 20, $"test/{Guid.NewGuid():N}", visibility, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow.AddMinutes(10));
        if (!pending) file.MarkAvailable(20, infected ? ScanStatus.Infected : ScanStatus.Clean);
        db.FileAssets.Add(file);
        await db.SaveChangesAsync();
        if (deleted)
        {
            db.FileAssets.Remove(file);
            await db.SaveChangesAsync();
        }
        return file.Id;
    }
}
