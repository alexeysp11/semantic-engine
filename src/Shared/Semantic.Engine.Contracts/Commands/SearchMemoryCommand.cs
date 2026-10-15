namespace Semantic.Engine.Contracts.Commands;

/// <summary>
/// Requests a semantic search over the memories of a session. Consumed by the Data Worker, which answers with a
/// <see cref="Events.ToolExecutedEvent"/> so that the search behaves like any other tool call of the agent.
/// </summary>
public sealed record SearchMemoryCommand
{
    /// <summary>Gets the identifier of the session whose memories are searched.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the identifier of the model tool call; echoed back in the result event.</summary>
    public string ToolCallId { get; init; } = string.Empty;

    /// <summary>Gets the tool arguments serialized as a JSON object with <c>query</c> and an optional <c>top_k</c>.</summary>
    public string ArgumentsJson { get; init; } = "{}";
}
