using FSH.Modules.Auditing;
using FSH.Modules.Auditing.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.SignalR;

namespace Auditing.Tests.Http;

/// <summary>
/// Regression guard: <see cref="AuditHttpMiddleware"/> buffers the response body into a MemoryStream
/// and only copies it to the socket after the handler returns. A long-lived streaming handler (SSE)
/// never returns, so buffering it left the client hanging forever on "connecting" with zero bytes.
/// Declared streaming endpoints and SignalR SSE transports must pass straight
/// through with the original response body intact.
/// </summary>
public sealed class AuditHttpMiddlewareStreamingTests
{
    private sealed class StubAuditPublisher : IAuditPublisher
    {
        public IAuditScope CurrentScope => throw new NotSupportedException("not used on the streaming path");
        public ValueTask PublishAsync(IAuditEvent auditEvent, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("text/event-stream")]
    [InlineData("text/event-stream, */*")]
    [InlineData("application/json, text/event-stream")]
    public async Task InvokeAsync_Should_NotBufferResponseBody_For_EventStreamRequests(string accept)
    {
        var ctx = new DefaultHttpContext();
        ctx.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new HubMetadata(typeof(Hub))), "hub"));
        ctx.Request.Headers.Accept = accept;
        using var originalBody = new MemoryStream();
        ctx.Response.Body = originalBody;

        Stream? bodySeenByHandler = null;
        Task Next(HttpContext c)
        {
            bodySeenByHandler = c.Response.Body;
            return Task.CompletedTask;
        }

        var middleware = new AuditHttpMiddleware(Next, new AuditHttpOptions(), new StubAuditPublisher());

        await middleware.InvokeAsync(ctx);

        // The handler must see the real response body, not a swapped-in audit buffer.
        bodySeenByHandler.ShouldBeSameAs(originalBody);
        ctx.Response.Body.ShouldBeSameAs(originalBody);
    }

    [Fact]
    public async Task DeclaredStream_Should_NotBuffer_Without_AcceptHeader()
    {
        var ctx = new DefaultHttpContext();
        ctx.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new StreamResponseMetadata()), "sse"));
        using var originalBody = new MemoryStream();
        ctx.Response.Body = originalBody;
        var middleware = new AuditHttpMiddleware(c =>
        {
            c.Response.Body.ShouldBeSameAs(originalBody);
            return Task.CompletedTask;
        }, new AuditHttpOptions(), new StubAuditPublisher());
        await middleware.InvokeAsync(ctx);
    }

    [Fact]
    public async Task AcceptHeader_Alone_Should_Not_Bypass_Auditing_For_OrdinaryEndpoint()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Accept = "text/event-stream";
        ctx.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(), "ordinary-api"));
        using var originalBody = new MemoryStream();
        ctx.Response.Body = originalBody;
        var middleware = new AuditHttpMiddleware(c =>
        {
            c.Response.Body.ShouldNotBeSameAs(originalBody);
            return Task.FromException(new IOException("Stop after verifying the capture path"));
        }, new AuditHttpOptions { MinExceptionSeverity = AuditSeverity.Critical }, new StubAuditPublisher());
        await Should.ThrowAsync<IOException>(() => middleware.InvokeAsync(ctx));
        ctx.Response.Body.ShouldBeSameAs(originalBody);
    }

    private sealed class StreamResponseMetadata : IProducesResponseTypeMetadata
    {
        public Type? Type => null;
        public int StatusCode => StatusCodes.Status200OK;
        public IEnumerable<string> ContentTypes => ["text/event-stream"];
    }
}
