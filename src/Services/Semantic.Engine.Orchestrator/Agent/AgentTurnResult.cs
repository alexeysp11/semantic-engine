namespace Semantic.Engine.Orchestrator.Agent;

/// <summary>
/// The outcome of one reasoning turn.
/// </summary>
public sealed class AgentTurnResult
{
    /// <summary>Gets the conversation history after the turn, including the assistant message.</summary>
    public string ConversationHistoryJson { get; init; } = string.Empty;

    /// <summary>Gets the tool call requested by the model, or <see langword="null"/> when the model produced a final answer.</summary>
    public RequestedToolCall? ToolCall { get; init; }

    /// <summary>Gets the failure reason, or <see langword="null"/> when the turn succeeded.</summary>
    public string? FailureReason { get; init; }
}
