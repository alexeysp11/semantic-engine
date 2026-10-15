using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Semantic.Engine.Orchestrator.Context;

/// <summary>
/// Serialization helpers for the conversation history persisted inside the saga state.
/// </summary>
public static class ConversationHistory
{
    /// <summary>Gets the serialized representation of an empty history.</summary>
    public const string Empty = "[]";

    /// <summary>
    /// Deserializes a history from JSON.
    /// </summary>
    /// <param name="json">The serialized history. Empty input yields an empty history.</param>
    /// <returns>A mutable list of chat messages.</returns>
    public static List<ChatMessage> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<ChatMessage>>(json, AIJsonUtilities.DefaultOptions) ?? [];
    }

    /// <summary>
    /// Serializes a history to JSON.
    /// </summary>
    /// <param name="messages">The messages to serialize.</param>
    /// <returns>The JSON representation, including polymorphic message content.</returns>
    public static string Serialize(IEnumerable<ChatMessage> messages)
    {
        return JsonSerializer.Serialize(messages.ToList(), AIJsonUtilities.DefaultOptions);
    }

    /// <summary>
    /// Appends a user message to a serialized history.
    /// </summary>
    /// <param name="historyJson">The serialized history.</param>
    /// <param name="prompt">The user prompt.</param>
    /// <returns>The updated serialized history.</returns>
    public static string AppendUserMessage(string historyJson, string prompt)
    {
        List<ChatMessage> messages = Deserialize(historyJson);
        messages.Add(new ChatMessage(ChatRole.User, prompt));
        return Serialize(messages);
    }

    /// <summary>
    /// Appends a tool result message to a serialized history.
    /// </summary>
    /// <param name="historyJson">The serialized history.</param>
    /// <param name="toolCallId">The identifier of the tool call the result answers.</param>
    /// <param name="output">The tool output or error description.</param>
    /// <returns>The updated serialized history.</returns>
    public static string AppendToolResult(string historyJson, string toolCallId, string output)
    {
        List<ChatMessage> messages = Deserialize(historyJson);
        messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(toolCallId, output)]));
        return Serialize(messages);
    }
}
