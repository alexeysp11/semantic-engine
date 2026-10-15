using Semantic.Engine.Client.Cli.Gateway;
using Spectre.Console;

namespace Semantic.Engine.Client.Cli.Rendering;

/// <summary>
/// Formats agent status events as spinner captions.
/// </summary>
public static class StatusText
{
    /// <summary>
    /// The caption shown before the first status event of a turn arrives.
    /// </summary>
    public const string Initial = "[yellow]Agent is thinking...[/]";

    /// <summary>
    /// Formats a status event.
    /// </summary>
    /// <param name="streamEvent">A status event.</param>
    /// <returns>Spectre markup of the caption.</returns>
    public static string Format(AgentStreamEvent streamEvent)
    {
        string detail = Markup.Escape(streamEvent.Detail ?? string.Empty);

        return streamEvent.Status switch
        {
            "ExecutingTool" => $"[blue]Executing {FormatTool(detail)}...[/]",
            "Summarizing" => "[grey]Summarizing conversation context...[/]",
            _ => Initial
        };
    }

    private static string FormatTool(string tool)
    {
        return tool switch
        {
            "dotnet_build" => "dotnet build",
            "dotnet_test" => "dotnet test",
            "" => "tool",
            _ => tool
        };
    }
}
