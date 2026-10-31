using Amazon.S3;

namespace FSH.Framework.Storage.S3;

/// <summary>
/// Signs presigned URLs for <see cref="S3StorageOptions.PublicServiceUrl"/>. Presigning is computed
/// locally, so this client never has to reach the public endpoint from inside the server.
/// <see cref="Client"/> is null when no separate public endpoint is configured.
/// </summary>
internal sealed class S3PublicEndpointClient(IAmazonS3? client) : IDisposable
{
    public IAmazonS3? Client { get; } = client;

    public void Dispose() => Client?.Dispose();
}
