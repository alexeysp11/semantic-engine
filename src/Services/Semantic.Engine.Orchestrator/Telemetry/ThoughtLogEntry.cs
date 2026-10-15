namespace Semantic.Engine.Orchestrator.Telemetry;

/// <summary>
/// One row of the ClickHouse <c>thought_logs</c> table that describes a step of the agent loop.
/// </summary>
public sealed class ThoughtLogEntry
{
    /// <summary>Gets the identifier of the session the step belongs to.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the step type; see <see cref="ThoughtStepTypes"/>.</summary>
    public string StepType { get; init; } = string.Empty;

    /// <summary>Gets the tool name for tool-related steps; empty otherwise.</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>Gets the content of the step: model text, tool arguments, tool output or a summary.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Gets the duration of the step in milliseconds; zero when not measured.</summary>
    public uint DurationMs { get; init; }
}
