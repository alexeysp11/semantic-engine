namespace Semantic.Engine.Orchestrator.Agent;

/// <summary>
/// A tool call that passed send-safety validation.
/// </summary>
/// <param name="CallId">The call identifier that the tool result must echo.</param>
/// <param name="Name">The tool name.</param>
/// <param name="ArgumentsJson">The arguments serialized as a JSON object.</param>
public sealed record RequestedToolCall(string CallId, string Name, string ArgumentsJson);
