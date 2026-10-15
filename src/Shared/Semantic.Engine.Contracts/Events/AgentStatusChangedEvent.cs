namespace Semantic.Engine.Contracts.Events;

/// <summary>
/// Notifies the client about a change of the agent processing state.
/// </summary>
public sealed record AgentStatusChangedEvent
{
    /// <summary>Gets the identifier of the session the status belongs to.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the new agent status.</summary>
    public AgentStatus Status { get; init; }

    /// <summary>Gets an optional human-readable detail, such as the tool name or the failure reason.</summary>
    public string? Detail { get; init; }
}
