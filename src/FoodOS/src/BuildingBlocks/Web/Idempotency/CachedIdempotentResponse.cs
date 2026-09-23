namespace FSH.Framework.Web.Idempotency;

/// <summary>
/// A cached HTTP response for idempotent replay.
/// </summary>
public sealed record CachedIdempotentResponse
{
    public int StatusCode { get; init; }
    public string? ContentType { get; init; }
    public byte[] Body { get; init; } = [];
}
