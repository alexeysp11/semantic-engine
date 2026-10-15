namespace Semantic.Engine.Contracts.Events;

/// <summary>
/// Reports that one reasoning iteration finished. Consumed by the Orchestrator saga.
/// </summary>
/// <remarks>
/// When <see cref="ToolName"/> is not <see langword="null"/> the model requested a tool call and the saga
/// must freeze the session until the matching <see cref="ToolExecutedEvent"/> arrives.
/// </remarks>
public sealed record AgentTurnCompletedEvent
{
    /// <summary>Gets the identifier of the session the turn belongs to.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the conversation history including the assistant message produced by the turn.</summary>
    public string ConversationHistoryJson { get; init; } = string.Empty;

    /// <summary>Gets the identifier of the requested tool call, or <see langword="null"/> when no tool is requested.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Gets the name of the requested tool, or <see langword="null"/> when no tool is requested.</summary>
    public string? ToolName { get; init; }

    /// <summary>Gets the requested tool arguments serialized as JSON, or <see langword="null"/> when no tool is requested.</summary>
    public string? ArgumentsJson { get; init; }

    /// <summary>Gets a failure description when the turn failed; <see langword="null"/> on success.</summary>
    public string? FailureReason { get; init; }
}
