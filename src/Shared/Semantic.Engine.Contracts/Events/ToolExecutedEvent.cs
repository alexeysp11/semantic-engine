namespace Semantic.Engine.Contracts.Events;

/// <summary>
/// Reports the outcome of an <see cref="Commands.ExecuteToolCommand"/>. Consumed by the Orchestrator saga.
/// </summary>
/// <remarks>
/// Tool failures, including compiler diagnostics, are reported through <see cref="Output"/> with
/// <see cref="Succeeded"/> set to <see langword="false"/> so that the model can self-correct.
/// </remarks>
public sealed record ToolExecutedEvent
{
    /// <summary>Gets the identifier of the session the tool ran for.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the identifier of the originating tool call.</summary>
    public string ToolCallId { get; init; } = string.Empty;

    /// <summary>Gets the name of the executed tool.</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the tool completed without errors.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Gets the tool output or the error description that is fed back to the model.</summary>
    public string Output { get; init; } = string.Empty;
}
