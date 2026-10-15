using MassTransit;

namespace Semantic.Engine.Contracts.Sagas;

/// <summary>
/// Persistent state of one agent session managed by the Orchestrator state machine.
/// </summary>
public sealed class AgentSessionState : SagaStateMachineInstance
{
    /// <summary>
    /// Gets or sets the session identifier shared by every message of the session.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the name of the current state machine state.
    /// </summary>
    public string CurrentState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized conversation history owned by the saga.
    /// </summary>
    public string ConversationHistoryJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the snapshot that is persisted when the agent is frozen while waiting for a tool result.
    /// Contains the pending tool call serialized as JSON; <see langword="null"/> when no tool call is pending.
    /// </summary>
    public string? LastStateSnapshot { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the session was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the last state change.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }
}
