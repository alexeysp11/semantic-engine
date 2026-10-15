namespace Semantic.Engine.Contracts.Events;

/// <summary>
/// Carries a fragment of the model output for real-time streaming to the client.
/// </summary>
public sealed record AgentTokenGeneratedEvent
{
    /// <summary>Gets the identifier of the session the token belongs to.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the text fragment produced by the model. Empty for the terminating event.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the agent finished the whole turn, including all tool calls.</summary>
    public bool IsFinal { get; init; }
}
