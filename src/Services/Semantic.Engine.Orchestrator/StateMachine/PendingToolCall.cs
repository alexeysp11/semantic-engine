using System.Text.Json;

namespace Semantic.Engine.Orchestrator.StateMachine;

/// <summary>
/// The tool call the frozen agent waits for. Serialized into <c>AgentSessionState.LastStateSnapshot</c>.
/// </summary>
/// <param name="ToolCallId">The identifier of the call.</param>
/// <param name="ToolName">The tool name.</param>
/// <param name="ArgumentsJson">The arguments serialized as a JSON object.</param>
public sealed record PendingToolCall(string ToolCallId, string ToolName, string ArgumentsJson)
{
    /// <summary>
    /// Serializes a pending call.
    /// </summary>
    /// <param name="call">The call to serialize.</param>
    /// <returns>The JSON snapshot.</returns>
    public static string Serialize(PendingToolCall call)
    {
        return JsonSerializer.Serialize(call);
    }

    /// <summary>
    /// Restores a pending call from a snapshot.
    /// </summary>
    /// <param name="snapshot">The persisted snapshot; <see langword="null"/> when no call is pending.</param>
    /// <returns>The call, or <see langword="null"/> when the snapshot is empty.</returns>
    public static PendingToolCall? Deserialize(string? snapshot)
    {
        return string.IsNullOrEmpty(snapshot) ? null : JsonSerializer.Deserialize<PendingToolCall>(snapshot);
    }

    /// <summary>
    /// Checks that a tool result answers the call stored in the snapshot.
    /// </summary>
    /// <param name="snapshot">The persisted snapshot; <see langword="null"/> when no call is pending.</param>
    /// <param name="toolCallId">The identifier reported by the Tool Worker.</param>
    /// <returns><see langword="true"/> when the identifiers match.</returns>
    public static bool Matches(string? snapshot, string toolCallId)
    {
        PendingToolCall? pending = Deserialize(snapshot);
        return pending is not null && pending.ToolCallId == toolCallId;
    }
}
