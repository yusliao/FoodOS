using Hangfire;

namespace Integration.Tests.Infrastructure;

// A secondary test host replaces Hangfire's process-wide storage reference.
// Restore the primary host's storage after the secondary host is disposed.
internal sealed class JobStorageScope : IDisposable
{
    private readonly JobStorage _original = JobStorage.Current;

    public void Dispose() => JobStorage.Current = _original;
}
