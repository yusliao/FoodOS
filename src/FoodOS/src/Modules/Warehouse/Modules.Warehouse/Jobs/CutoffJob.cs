using Microsoft.Extensions.Logging;

namespace FSH.Modules.Warehouse.Jobs;

/// <summary>
/// Compatibility entry point for already queued legacy Hangfire jobs.
/// External WMS owns warehouse execution; never resume the local cutoff chain.
/// Keep the type and method signature until persisted jobs have been drained.
/// </summary>
public sealed class CutoffJob(ILogger<CutoffJob> logger)
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("[Warehouse] legacy cutoff job skipped: external WMS owns warehouse execution");
        return Task.CompletedTask;
    }
}
