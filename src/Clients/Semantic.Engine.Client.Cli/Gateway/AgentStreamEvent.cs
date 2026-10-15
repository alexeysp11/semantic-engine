namespace Semantic.Engine.Client.Cli.Gateway;

/// <summary>
/// An event received from the session stream of the Gateway.
/// </summary>
public sealed class AgentStreamEvent
{
    /// <summary>Gets the kind of the event.</summary>
    public AgentStreamEventKind Kind { get; init; }

    /// <summary>Gets the text fragment for <see cref="AgentStreamEventKind.Token"/> events.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Gets the status name for <see cref="AgentStreamEventKind.Status"/> events, for example <c>Thinking</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets the optional status detail, such as the tool name or the failure reason.</summary>
    public string? Detail { get; init; }

    /// <summary>Gets a value indicating whether this event reports a failed turn.</summary>
    public bool IsFailure => Kind == AgentStreamEventKind.Status && Status == "Failed";
}

/// <summary>
/// The kinds of session stream events.
/// </summary>
public enum AgentStreamEventKind
{
    /// <summary>A fragment of the model output.</summary>
    Token = 0,

    /// <summary>A change of the agent status.</summary>
    Status = 1,

    /// <summary>The agent finished the turn.</summary>
    Done = 2
}
