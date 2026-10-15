using Microsoft.Extensions.Options;
using Semantic.Engine.Gateway.Options;

namespace Semantic.Engine.Gateway.Streaming;

/// <summary>
/// Periodically removes session streams that no client claimed, so that buffered events cannot accumulate forever.
/// </summary>
public sealed class SessionStreamSweeper(
    SessionStreamHub hub,
    IOptions<StreamingOptions> options,
    ILogger<SessionStreamSweeper> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan timeToLive = TimeSpan.FromMinutes(options.Value.UnclaimedStreamTtlMinutes);
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(options.Value.SweepIntervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            int removed = hub.RemoveExpired(timeToLive);
            if (removed > 0)
            {
                logger.LogInformation("Removed {Count} unclaimed session stream(s)", removed);
            }
        }
    }
}
