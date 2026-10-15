namespace Semantic.Engine.Contracts.Commands;

/// <summary>
/// Requests one reasoning iteration of the agent loop. Sent by the saga to the Orchestrator reasoning consumer.
/// </summary>
public sealed record RunAgentTurnCommand
{
    /// <summary>Gets the identifier of the session the turn belongs to.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the serialized conversation history that the model must continue.</summary>
    public string ConversationHistoryJson { get; init; } = string.Empty;
}
