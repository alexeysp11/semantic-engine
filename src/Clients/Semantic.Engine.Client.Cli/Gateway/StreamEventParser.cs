using System.Text.Json;

namespace Semantic.Engine.Client.Cli.Gateway;

/// <summary>
/// Converts raw Server-Sent Events of the Gateway into <see cref="AgentStreamEvent"/> instances.
/// </summary>
public static class StreamEventParser
{
    /// <summary>
    /// Parses one SSE event.
    /// </summary>
    /// <param name="eventType">The SSE event name: <c>token</c>, <c>status</c> or <c>done</c>.</param>
    /// <param name="data">The JSON data line of the event.</param>
    /// <returns>The parsed event, or <see langword="null"/> for unknown event names.</returns>
    public static AgentStreamEvent? Parse(string eventType, string data)
    {
        switch (eventType)
        {
            case "token":
                using (JsonDocument token = JsonDocument.Parse(data))
                {
                    return new AgentStreamEvent
                    {
                        Kind = AgentStreamEventKind.Token,
                        Text = token.RootElement.GetProperty("text").GetString() ?? string.Empty
                    };
                }

            case "status":
                using (JsonDocument status = JsonDocument.Parse(data))
                {
                    JsonElement detail = status.RootElement.GetProperty("detail");
                    return new AgentStreamEvent
                    {
                        Kind = AgentStreamEventKind.Status,
                        Status = status.RootElement.GetProperty("status").GetString() ?? string.Empty,
                        Detail = detail.ValueKind == JsonValueKind.String ? detail.GetString() : null
                    };
                }

            case "done":
                return new AgentStreamEvent { Kind = AgentStreamEventKind.Done };

            default:
                return null;
        }
    }
}
