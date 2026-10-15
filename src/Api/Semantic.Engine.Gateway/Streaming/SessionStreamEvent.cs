namespace Semantic.Engine.Gateway.Streaming;

/// <summary>
/// One Server-Sent Event delivered to a client.
/// </summary>
public sealed class SessionStreamEvent
{
    /// <summary>The SSE event name of a model text fragment.</summary>
    public const string TokenEvent = "token";

    /// <summary>The SSE event name of an agent status change.</summary>
    public const string StatusEvent = "status";

    /// <summary>The SSE event name that marks the end of an agent turn.</summary>
    public const string DoneEvent = "done";

    /// <summary>Gets the SSE event name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the event payload serialized as a single-line JSON document.</summary>
    public string DataJson { get; init; } = "{}";
}
