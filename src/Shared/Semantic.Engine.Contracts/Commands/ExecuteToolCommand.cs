namespace Semantic.Engine.Contracts.Commands;

/// <summary>
/// Requests execution of a tool inside the isolated Tool Worker.
/// </summary>
public sealed record ExecuteToolCommand
{
    /// <summary>Gets the identifier of the session that owns the workspace the tool operates on.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the identifier of the model tool call; echoed back in <see cref="Events.ToolExecutedEvent"/>.</summary>
    public string ToolCallId { get; init; } = string.Empty;

    /// <summary>Gets the tool name as declared in the tool catalog, for example <c>read_file</c>.</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>Gets the tool arguments serialized as a JSON object.</summary>
    public string ArgumentsJson { get; init; } = "{}";
}
