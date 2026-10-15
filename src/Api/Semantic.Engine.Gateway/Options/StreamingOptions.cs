using System.ComponentModel.DataAnnotations;

namespace Semantic.Engine.Gateway.Options;

/// <summary>
/// Settings of the SSE bridge. Bound from the <c>Streaming</c> configuration section and validated on startup.
/// </summary>
public sealed class StreamingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Streaming";

    /// <summary>Gets or sets the interval of keep-alive comments sent to idle SSE connections, in seconds.</summary>
    [Range(1, 600)]
    public int HeartbeatSeconds { get; set; }

    /// <summary>Gets or sets how long events of a session are buffered while no client is connected, in minutes.</summary>
    [Range(1, 1440)]
    public int UnclaimedStreamTtlMinutes { get; set; }

    /// <summary>Gets or sets the interval of the cleanup of unclaimed streams, in minutes.</summary>
    [Range(1, 1440)]
    public int SweepIntervalMinutes { get; set; }
}
