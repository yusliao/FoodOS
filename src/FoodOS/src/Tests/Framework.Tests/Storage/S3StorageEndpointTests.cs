using FSH.Framework.Storage;
using FSH.Framework.Storage.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Framework.Tests.Storage;

/// <summary>
/// The API talks to MinIO on a private address while browsers use a public proxy. Presigned URLs and
/// public links must carry the public host; storage errors must not look like a missing upload.
/// </summary>
public sealed class S3StorageEndpointTests
{
    private const string PublicUrl = "http://203.0.113.5:18083";
    private const string Key = "tenants/root/user/2026/10/abc/avatar.png";

    private static ServiceProvider Build(string serviceUrl, string? publicUrl)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "s3",
            ["Storage:S3:Bucket"] = "foodos",
            ["Storage:S3:Region"] = "us-east-1",
            ["Storage:S3:ServiceUrl"] = serviceUrl,
            ["Storage:S3:PublicServiceUrl"] = publicUrl,
            ["Storage:S3:AccessKey"] = "foodos",
            ["Storage:S3:SecretKey"] = "not-a-real-secret",
            ["Storage:S3:ForcePathStyle"] = "true",
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddHeroStorage(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PresignedUrls_Should_UsePublicEndpoint_When_Configured()
    {
        await using var provider = Build("http://checkpay-minio:9000", PublicUrl);
        var storage = provider.GetRequiredService<IStorageService>();

        var upload = await storage.GenerateUploadUrlAsync(Key, "image/png", 1024, TimeSpan.FromMinutes(5));
        var download = await storage.GenerateDownloadUrlAsync(Key, TimeSpan.FromMinutes(5));

        upload.Url.GetLeftPart(UriPartial.Authority).ShouldBe(PublicUrl);
        upload.Url.AbsolutePath.ShouldBe($"/foodos/{Key}");
        download.GetLeftPart(UriPartial.Authority).ShouldBe(PublicUrl);
        storage.BuildPublicUrl(Key).ShouldBe($"{PublicUrl}/foodos/{Key}");
    }

    [Fact]
    public async Task PresignedUrls_Should_UseServiceEndpoint_When_NoPublicEndpoint()
    {
        await using var provider = Build("http://localhost:9000", publicUrl: null);
        var storage = provider.GetRequiredService<IStorageService>();

        var upload = await storage.GenerateUploadUrlAsync(Key, "image/png", 1024, TimeSpan.FromMinutes(5));

        upload.Url.GetLeftPart(UriPartial.Authority).ShouldBe("http://localhost:9000");
        storage.BuildPublicUrl(Key).ShouldBe($"http://localhost:9000/foodos/{Key}");
    }

    [Fact]
    public async Task HeadObject_Should_Throw_When_StorageIsUnreachable()
    {
        // Port 9 (discard) refuses connections: HEAD must fail loudly, not report "upload not received".
        await using var provider = Build("http://127.0.0.1:9", PublicUrl);
        var storage = provider.GetRequiredService<IStorageService>();

        await Should.ThrowAsync<Exception>(() => storage.HeadObjectAsync(Key));
    }
}
