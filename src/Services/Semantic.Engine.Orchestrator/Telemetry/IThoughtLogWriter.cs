namespace Semantic.Engine.Orchestrator.Telemetry;

/// <summary>
/// Persists reasoning traces of the agent into an analytical store.
/// </summary>
public interface IThoughtLogWriter
{
    /// <summary>
    /// Writes one thought log entry.
    /// </summary>
    /// <param name="entry">The entry to store.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <remarks>
    /// Implementations must not throw on storage failures: thought logging is a diagnostic side channel
    /// and must never break the agent loop.
    /// </remarks>
    Task WriteAsync(ThoughtLogEntry entry, CancellationToken cancellationToken);
}
