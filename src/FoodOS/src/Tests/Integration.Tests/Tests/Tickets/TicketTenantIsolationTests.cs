using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Domain;
using FSH.Modules.Files.Contracts.v1.DTOs;
using FSH.Modules.Files.Contracts.Authorization;
using FSH.Modules.Files.Data;
using FSH.Modules.Files.Domain;
using FSH.Modules.Multitenancy.Contracts.Dtos;
using Microsoft.AspNetCore.Identity;
using FSH.Modules.Tickets.Contracts.Authorization;
using SupportTicketDto = FSH.Modules.Tickets.Contracts.Dtos.TicketDto;
using SupportCommentDto = FSH.Modules.Tickets.Contracts.Dtos.TicketCommentDto;

namespace Integration.Tests.Tests.Tickets;

/// <summary>
/// Cross-TENANT isolation for the tickets module. Proves a ticket created in
/// tenant A (root) is invisible to tenant B: B cannot fetch it, list it, or
/// mutate it (assign). Reads return 404; operator-only assignment returns 403.
/// TicketsDbContext restricts customers by explicit ownership while permitting operator support.
/// Intra-tenant lifecycle / state-machine
/// coverage lives in <see cref="TicketsEndpointTests"/>.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class TicketTenantIsolationTests
{
    private readonly AuthHelper _auth;
    private readonly FshWebApplicationFactory _factory;

    public TicketTenantIsolationTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task GetTicketById_Should_Return404_When_OwnedByDifferentTenant()
    {
        // Arrange — tenant A (root) creates a ticket; tenant B is freshly provisioned.
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        using var otherClient = await ProvisionTenantClientAsync(rootClient, $"ticket-get-{uniqueId}");

        var ticketId = await CreateTicketAsync(rootClient, $"Ticket-RootOnly-{uniqueId}");

        // Act — tenant B tries to fetch tenant A's ticket.
        using var crossGet = await otherClient.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets/{ticketId}");

        // Assert — clean 404, never tenant A's data.
        crossGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Sanity: tenant A still sees its own ticket.
        using var ownGet = await rootClient.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets/{ticketId}");
        ownGet.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchTickets_Should_NotReturn_OtherTenants_Tickets()
    {
        // Arrange.
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        using var otherClient = await ProvisionTenantClientAsync(rootClient, $"ticket-list-{uniqueId}");

        var rootTitle = $"Ticket-RootOnly-{uniqueId}";
        var ticketId = await CreateTicketAsync(rootClient, rootTitle);

        // Act — tenant B lists tickets.
        using var listResponse = await otherClient.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets?pageNumber=1&pageSize=200");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await listResponse.DeserializeAsync<PagedResult<TicketDto>>();
        var body = await otherClient.GetStringAsync(
            $"{TestConstants.TicketsBasePath}/tickets?pageNumber=1&pageSize=200");

        // Assert — tenant A's ticket never appears in tenant B's listing.
        page.Items.ShouldNotContain(t => t.Id == ticketId,
            "tenant B's ticket list must not include tenant A's ticket");
        body.ShouldNotContain(rootTitle);
    }

    [Fact]
    public async Task AssignTicket_Should_Return403_ForCustomerTenant()
    {
        // Arrange.
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        using var otherClient = await ProvisionTenantClientAsync(rootClient, $"ticket-mut-{uniqueId}");

        var ticketId = await CreateTicketAsync(rootClient, $"Ticket-Mutate-{uniqueId}");

        // Act — tenant B tries to mutate (assign) tenant A's ticket.
        using var crossAssign = await otherClient.PostAsJsonAsync(
            $"{TestConstants.TicketsBasePath}/tickets/{ticketId}/assign",
            new { assigneeUserId = Guid.NewGuid() });

        // Customer tenants cannot use the operator assignment action at all.
        crossAssign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Sanity: tenant A's ticket is untouched — still Open, no assignee.
        using var ownGet = await rootClient.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets/{ticketId}");
        ownGet.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await ownGet.DeserializeAsync<TicketDto>();
        fetched.Status.ShouldBe("Open");
        fetched.AssignedToUserId.ShouldBeNull();
    }

    [Fact]
    public async Task CustomerMember_Should_Not_Read_AnotherMembersTicket_WithinSameTenant()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string tenantId = $"ticket-member-{suffix}";
        string adminEmail = $"{tenantId}-admin@tenant.com";
        await CreateTenantAsync(rootClient, tenantId, adminEmail);
        await WaitForProvisioningAsync(rootClient, tenantId);
        using var tenantAdmin = await CreateTenantAdminClientWithRetryAsync(
            adminEmail,
            TestConstants.DefaultPassword,
            tenantId);

        string memberEmail = $"member-{suffix}@tenant.com";
        Guid memberId = await CreateActiveBasicUserAsync(tenantId, memberEmail, $"member-{suffix}");
        using var member = await CreateTenantAdminClientWithRetryAsync(
            memberEmail,
            TestConstants.DefaultPassword,
            tenantId);

        using var create = await tenantAdmin.PostAsJsonAsync($"{TestConstants.TicketsBasePath}/tickets", new
        {
            title = $"Private participant ticket {suffix}",
            description = "Only the reporter or an assigned participant may see this.",
            priority = "Medium",
            assignedToUserId = memberId,
        });
        create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Guid ticketId = await CreateTicketAsync(tenantAdmin, $"Private participant ticket {suffix}");

        using var adminDetail = await tenantAdmin.GetAsync($"{TestConstants.TicketsBasePath}/tickets/{ticketId}");
        var ownTicket = await adminDetail.DeserializeAsync<TicketDto>();
        ownTicket.AssignedToUserId.ShouldBeNull("customer callers cannot appoint an arbitrary assignee");

        using var foreignDetail = await member.GetAsync($"{TestConstants.TicketsBasePath}/tickets/{ticketId}");
        foreignDetail.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var foreignComments = await member.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets/{ticketId}/comments");
        foreignComments.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var foreignList = await member.GetAsync(
            $"{TestConstants.TicketsBasePath}/tickets?pageNumber=1&pageSize=200");
        foreignList.StatusCode.ShouldBe(HttpStatusCode.OK, await foreignList.Content.ReadAsStringAsync());
        (await foreignList.DeserializeAsync<PagedResult<TicketDto>>()).Items.ShouldNotContain(
            ticket => ticket.Id == ticketId);

        object Attachment(int visibility) => new
        {
            ownerType = "Ticket", ownerId = ticketId, fileName = "evidence.png",
            contentType = "image/png", sizeBytes = 128, visibility, category = "Image",
        };

        using var publicUpload = await tenantAdmin.PostAsJsonAsync("/api/v1/files/upload-url", Attachment(0));
        publicUpload.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var upload = await tenantAdmin.PostAsJsonAsync("/api/v1/files/upload-url", Attachment(1));
        upload.StatusCode.ShouldBe(HttpStatusCode.OK, await upload.Content.ReadAsStringAsync());
        Guid fileId = (await upload.DeserializeAsync<PresignedUploadResponse>()).FileAssetId;
        using var ownDownload = await tenantAdmin.GetAsync($"/api/v1/files/{fileId}/url");
        ownDownload.StatusCode.ShouldBe(HttpStatusCode.NotFound, "pending attachments must not receive download URLs");
        using var ownMetadata = await tenantAdmin.GetAsync($"/api/v1/files/{fileId}");
        ownMetadata.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var foreignUpload = await member.PostAsJsonAsync("/api/v1/files/upload-url", Attachment(1));
        foreignUpload.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var foreignMetadata = await member.GetAsync($"/api/v1/files/{fileId}");
        foreignMetadata.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var foreignDownload = await member.GetAsync($"/api/v1/files/{fileId}/url");
        foreignDownload.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var foreignDelete = await member.DeleteAsync($"/api/v1/files/{fileId}");
        foreignDelete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var publish = await tenantAdmin.PatchAsJsonAsync(
            $"/api/v1/files/{fileId}/visibility", new { visibility = 0 });
        publish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var deleteTicket = await tenantAdmin.DeleteAsync($"{TestConstants.TicketsBasePath}/tickets/{ticketId}");
        deleteTicket.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var deletedTicketDownload = await tenantAdmin.GetAsync($"/api/v1/files/{fileId}/url");
        deletedTicketDownload.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OperatorSupport_Should_Collaborate_WithCustomers_Without_Exposing_OtherCustomersTickets()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string tenantA = $"support-a-{suffix}";
        string tenantB = $"support-b-{suffix}";
        using var customerA = await ProvisionTenantClientAsync(admin, tenantA);
        using var customerB = await ProvisionTenantClientAsync(admin, tenantB);
        Guid ticketA = await CreateTicketAsync(customerA, $"Support-A-{suffix}");
        Guid ticketB = await CreateTicketAsync(customerB, $"Support-B-{suffix}");

        string email = $"support-{suffix}@test.com";
        Guid staffId = await CreateActiveBasicUserAsync("root", email, $"support-{suffix}", assignBasic: false);
        using var unprivileged = await _auth.CreateAuthenticatedClientAsync(email, TestConstants.DefaultPassword);
        using var denied = await unprivileged.GetAsync($"/api/v1/tickets/{ticketA}");
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var roleResponse = await admin.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/roles", new
        {
            id = string.Empty, name = $"Support-{suffix}", description = "Customer support test role",
        });
        roleResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var role = await roleResponse.DeserializeAsync<RoleDto>();
        using var permissions = await admin.PutAsJsonAsync($"{TestConstants.IdentityBasePath}/{role.Id}/permissions", new
        {
            roleId = role.Id,
            permissions = new[] { TicketsPermissions.Tickets.View, TicketsPermissions.Tickets.Comment,
                TicketsPermissions.Tickets.Create, TicketsPermissions.Tickets.Resolve },
        });
        permissions.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var assignRole = await admin.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/users/{staffId}/roles", new
        {
            userId = staffId.ToString(), userRoles = new[] { new { roleName = role.Name, enabled = true } },
        });
        assignRole.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var support = await _auth.CreateAuthenticatedClientAsync(email, TestConstants.DefaultPassword);
        using var list = await support.GetAsync($"/api/v1/tickets?search={suffix}&pageSize=200");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tickets = (await list.DeserializeAsync<PagedResult<SupportTicketDto>>()).Items;
        tickets.Single(ticket => ticket.Id == ticketA).CustomerTenantId.ShouldBe(tenantA);
        tickets.Single(ticket => ticket.Id == ticketB).CustomerTenantId.ShouldBe(tenantB);
        tickets.Single(ticket => ticket.Id == ticketA).Number.ShouldBe("TK-1");
        tickets.Single(ticket => ticket.Id == ticketB).Number.ShouldBe("TK-1");

        using var createWithoutAssign = await support.PostAsJsonAsync("/api/v1/tickets", new
        {
            title = "Create must not bypass assignment permission", priority = "Medium", assignedToUserId = staffId,
        });
        createWithoutAssign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var grantAssign = await admin.PutAsJsonAsync($"{TestConstants.IdentityBasePath}/{role.Id}/permissions", new
        {
            roleId = role.Id,
            permissions = new[] { TicketsPermissions.Tickets.View, TicketsPermissions.Tickets.Comment,
                TicketsPermissions.Tickets.Assign, TicketsPermissions.Tickets.Resolve, TicketsPermissions.Tickets.Create },
        });
        grantAssign.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var profileB = await customerB.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        string userB = (await profileB.DeserializeAsync<UserDto>()).Id;
        using var invalidCreate = await admin.PostAsJsonAsync("/api/v1/tickets", new
        {
            title = "Cannot assign customer as operator", priority = "Medium", assignedToUserId = userB,
        });
        invalidCreate.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var foreignAssign = await support.PostAsJsonAsync($"/api/v1/tickets/{ticketA}/assign", new { assigneeUserId = userB });
        foreignAssign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var assign = await support.PostAsJsonAsync($"/api/v1/tickets/{ticketA}/assign", new { assigneeUserId = staffId });
        assign.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var reply = await support.PostAsJsonAsync($"/api/v1/tickets/{ticketA}/comments", new { body = "Operator response for A only" });
        reply.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var customerReply = await customerA.PostAsJsonAsync($"/api/v1/tickets/{ticketA}/comments", new { body = "Restaurant A confirmation" });
        customerReply.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var comments = await customerA.GetAsync($"/api/v1/tickets/{ticketA}/comments");
        comments.StatusCode.ShouldBe(HttpStatusCode.OK);
        var visibleComments = await comments.DeserializeAsync<IReadOnlyList<SupportCommentDto>>();
        visibleComments.Count.ShouldBe(2);
        visibleComments.ShouldContain(comment => comment.AuthorUserId == staffId);

        foreach (var attempt in new[] { (customerB, ticketA), (customerA, ticketB) })
        {
            using var detail = await attempt.Item1.GetAsync($"/api/v1/tickets/{attempt.Item2}");
            detail.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            using var readComments = await attempt.Item1.GetAsync($"/api/v1/tickets/{attempt.Item2}/comments");
            readComments.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            using var writeComment = await attempt.Item1.PostAsJsonAsync($"/api/v1/tickets/{attempt.Item2}/comments", new { body = "Intrusion" });
            writeComment.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            using var delete = await attempt.Item1.DeleteAsync($"/api/v1/tickets/{attempt.Item2}");
            delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using var customerResolve = await customerA.PostAsJsonAsync($"/api/v1/tickets/{ticketA}/resolve", new { resolutionNote = "Cannot self-resolve" });
        customerResolve.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var resolve = await support.PostAsJsonAsync($"/api/v1/tickets/{ticketA}/resolve", new { resolutionNote = "Replaced missing delivery" });
        resolve.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var close = await customerA.PostAsJsonAsync($"/api/v1/tickets/{ticketA}/close", new { });
        close.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var closed = await support.GetAsync($"/api/v1/tickets/{ticketA}");
        (await closed.DeserializeAsync<TicketDto>()).Status.ShouldBe("Closed");
        using var deleteOwn = await customerA.DeleteAsync($"/api/v1/tickets/{ticketA}");
        deleteOwn.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var foreignRestore = await customerB.PostAsJsonAsync($"/api/v1/tickets/{ticketA}/restore", new { });
        foreignRestore.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var restore = await admin.PostAsJsonAsync($"/api/v1/tickets/{ticketA}/restore", new { });
        restore.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var restored = await customerA.GetAsync($"/api/v1/tickets/{ticketA}");
        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TicketAttachments_Should_AllowAuthorizedCrossTenantReads_WithoutSharingOtherFilesOrWrites()
    {
        using var admin = await _auth.CreateRootAdminClientAsync();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string tenantA = $"attach-a-{suffix}";
        string tenantB = $"attach-b-{suffix}";
        using var customerA = await ProvisionTenantClientAsync(admin, tenantA);
        using var customerB = await ProvisionTenantClientAsync(admin, tenantB);
        using var support = await OperatorTestUsers.CreateOperatorAsync(_factory,
            TicketsPermissions.Tickets.View, FilesPermissions.Upload, FilesPermissions.DeleteOwn);
        using var unprivileged = await OperatorTestUsers.CreateOperatorAsync(_factory, FilesPermissions.Upload);
        string memberEmail = $"member-{suffix}@test.com";
        await CreateActiveBasicUserAsync(tenantA, memberEmail, $"member-{suffix}");
        using var member = await _auth.CreateAuthenticatedClientAsync(memberEmail, TestConstants.DefaultPassword, tenantA);
        Guid ticket = await CreateTicketAsync(customerA, $"Attachment exchange {suffix}");
        Guid customerFile = await UploadTicketAttachmentAsync(customerA, ticket, support);
        Guid operatorFile = await UploadTicketAttachmentAsync(support, ticket, customerA);
        Guid mismatchedFile;
        using (var scope = _factory.Services.CreateScope())
        {
            // A malformed legacy row must not become visible merely because its OwnerId names an accessible ticket.
            var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync(tenantB);
            scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
                new MultiTenantContext<AppTenantInfo>(tenant);
            var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
            var file = FileAsset.CreatePending(Guid.NewGuid(), "Ticket", ticket, "wrong-source.pdf", "wrong-source.pdf",
                "application/pdf", 20, $"test/{Guid.NewGuid():N}", Visibility.Private,
                (await WaveAssignments.UserIdAsync(customerB)).ToString(), DateTimeOffset.UtcNow.AddMinutes(10));
            file.MarkAvailable(20, ScanStatus.Clean);
            db.FileAssets.Add(file);
            await db.SaveChangesAsync();
            mismatchedFile = file.Id;
        }
        foreach (var reader in new[] { support, customerA, customerB })
        {
            using var mismatch = await reader.GetAsync($"/api/v1/files/{mismatchedFile}/url");
            mismatch.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
        foreach (var (reader, fileId) in new[] { (support, customerFile), (customerA, operatorFile) })
        {
            using var metadata = await reader.GetAsync($"/api/v1/files/{fileId}");
            metadata.StatusCode.ShouldBe(HttpStatusCode.OK, await metadata.Content.ReadAsStringAsync());
            var file = await metadata.DeserializeAsync<FileAssetDto>();
            file.PublicUrl.ShouldBeNull();
            file.Visibility.ShouldBe(Visibility.Private);
            using var download = await reader.GetAsync($"/api/v1/files/{fileId}/url");
            download.StatusCode.ShouldBe(HttpStatusCode.OK);
            var url = await download.DeserializeAsync<PresignedDownloadResponse>();
            using var raw = new HttpClient();
            (await raw.GetStringAsync(url.Url)).ShouldBe("Private ticket evidence");
            using var finalize = await reader.PostAsync($"/api/v1/files/{fileId}/finalize", null);
            finalize.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            using var delete = await reader.DeleteAsync($"/api/v1/files/{fileId}");
            delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
        foreach (var other in new[] { customerB, member, unprivileged })
        {
            foreach (var fileId in new[] { customerFile, operatorFile })
            {
                using var metadata = await other.GetAsync($"/api/v1/files/{fileId}");
                metadata.StatusCode.ShouldBe(HttpStatusCode.NotFound);
                using var url = await other.GetAsync($"/api/v1/files/{fileId}/url");
                url.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }
        }
        using var visibility = await support.PatchAsJsonAsync($"/api/v1/files/{operatorFile}/visibility", new { visibility = 0 });
        visibility.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var ownDelete = await customerA.DeleteAsync($"/api/v1/files/{customerFile}");
        ownDelete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var deletedFile = await support.GetAsync($"/api/v1/files/{customerFile}/url");
        deletedFile.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var deletedTicket = await customerA.DeleteAsync($"/api/v1/tickets/{ticket}");
        deletedTicket.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var revokedAccess = await customerA.GetAsync($"/api/v1/files/{operatorFile}/url");
        revokedAccess.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var revokedSupport = await support.GetAsync($"/api/v1/files/{operatorFile}/url");
        revokedSupport.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> UploadTicketAttachmentAsync(HttpClient uploader, Guid ticketId, HttpClient collaborator)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes("Private ticket evidence");
        using var request = await uploader.PostAsJsonAsync("/api/v1/files/upload-url", new
        {
            ownerType = "Ticket", ownerId = ticketId, fileName = "evidence.pdf", contentType = "application/pdf",
            sizeBytes = bytes.Length, visibility = 1, category = "Document",
        });
        request.StatusCode.ShouldBe(HttpStatusCode.OK, await request.Content.ReadAsStringAsync());
        var upload = await request.DeserializeAsync<PresignedUploadResponse>();
        using var pending = await collaborator.GetAsync($"/api/v1/files/{upload.FileAssetId}/url");
        pending.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var raw = new HttpClient();
        using var put = new HttpRequestMessage(HttpMethod.Put, upload.UploadUrl)
        {
            Content = new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") } },
        };
        using var uploaded = await raw.SendAsync(put);
        uploaded.EnsureSuccessStatusCode();
        using var finalize = await uploader.PostAsync($"/api/v1/files/{upload.FileAssetId}/finalize", null);
        finalize.EnsureSuccessStatusCode();
        return upload.FileAssetId;
    }

    private async Task<Guid> CreateActiveBasicUserAsync(string tenantId, string email, string userName, bool assignBasic = true)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(tenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var user = new FshUser
        {
            FirstName = "Ticket",
            LastName = "Member",
            Email = email,
            UserName = userName,
            EmailConfirmed = true,
            IsActive = true,
        };
        var created = await userManager.CreateAsync(user, TestConstants.DefaultPassword);
        created.Succeeded.ShouldBeTrue(string.Join(", ", created.Errors.Select(error => error.Description)));
        if (assignBasic)
        {
            var assigned = await userManager.AddToRoleAsync(user, RoleConstants.Basic);
            assigned.Succeeded.ShouldBeTrue(string.Join(", ", assigned.Errors.Select(error => error.Description)));
        }
        return Guid.Parse(user.Id);
    }

    // ─── helpers ─────────────────────────────────────────────────────

    private static async Task<Guid> CreateTicketAsync(HttpClient client, string title)
    {
        using var response = await client.PostAsJsonAsync($"{TestConstants.TicketsBasePath}/tickets", new
        {
            title,
            description = (string?)null,
            priority = "Medium",
            assignedToUserId = (Guid?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            $"setup failed to create ticket: {await response.Content.ReadAsStringAsync()}");
        return await response.DeserializeAsync<Guid>();
    }

    private async Task<HttpClient> ProvisionTenantClientAsync(HttpClient rootClient, string tenantId)
    {
        var adminEmail = $"{tenantId}-admin@tenant.com";
        await CreateTenantAsync(rootClient, tenantId, adminEmail);
        await WaitForProvisioningAsync(rootClient, tenantId);
        return await CreateTenantAdminClientWithRetryAsync(
            adminEmail, TestConstants.DefaultPassword, tenantId);
    }

    private async Task<HttpClient> CreateTenantAdminClientWithRetryAsync(
        string email, string password, string tenant, int maxRetries = 30)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await _auth.CreateAuthenticatedClientAsync(email, password, tenant);
            }
            catch (HttpRequestException) when (i < maxRetries - 1)
            {
                await Task.Delay(1000);
            }
        }

        return await _auth.CreateAuthenticatedClientAsync(email, password, tenant);
    }

    private static async Task CreateTenantAsync(HttpClient rootClient, string tenantId, string adminEmail)
    {
        var response = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"Tenant {tenantId}",
            connectionString = (string?)null,
            adminEmail,
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer"
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, $"Create tenant failed: {body}");
    }

    private static async Task WaitForProvisioningAsync(HttpClient client, string tenantId, int maxRetries = 60)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var statusResponse = await client.GetAsync(
                $"{TestConstants.TenantsBasePath}/{tenantId}/provisioning");

            if (statusResponse.IsSuccessStatusCode)
            {
                var content = await statusResponse.Content.ReadAsStringAsync();
                var status = (await statusResponse.DeserializeAsync<TenantProvisioningStatusDto>()).Status;
                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Tenant {tenantId} provisioning failed: {content}");
                }
            }

            await Task.Delay(1000);
        }

        var finalResponse = await client.GetAsync(
            $"{TestConstants.TenantsBasePath}/{tenantId}/provisioning");
        var finalContent = finalResponse.IsSuccessStatusCode
            ? await finalResponse.Content.ReadAsStringAsync()
            : $"HTTP {finalResponse.StatusCode}";

        throw new TimeoutException(
            $"Tenant {tenantId} provisioning did not complete within {maxRetries} seconds. Last status: {finalContent}");
    }
}
