namespace Semantic.Engine.Contracts.Commands;

/// <summary>
/// Delivers a subsequent user prompt into an existing agent session.
/// </summary>
/// <remarks>
/// The saga accepts the command only while the session is idle. A prompt sent while the agent is
/// still reasoning or waiting for a tool result is rejected with a <see cref="Events.AgentStatusChangedEvent"/>.
/// </remarks>
public sealed record SubmitAgentPromptCommand
{
    /// <summary>Gets the identifier of the target session.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the user prompt. Must not be empty.</summary>
    public string Prompt { get; init; } = string.Empty;
}
