using System.ComponentModel.DataAnnotations;

namespace Semantic.Engine.Orchestrator.Options;

/// <summary>
/// Settings of the watchdog that unfreezes sessions waiting for a tool result that never arrives.
/// Bound from the <c>ToolWatchdog</c> configuration section and validated on startup.
/// </summary>
public sealed class ToolWatchdogOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ToolWatchdog";

    /// <summary>Gets or sets how long a session may wait for a tool result before the call is reported as timed out, in seconds.</summary>
    [Range(10, 86400)]
    public int ToolTimeoutSeconds { get; set; }

    /// <summary>Gets or sets the interval between scans for waiting sessions, in seconds.</summary>
    [Range(1, 3600)]
    public int ScanIntervalSeconds { get; set; }
}
