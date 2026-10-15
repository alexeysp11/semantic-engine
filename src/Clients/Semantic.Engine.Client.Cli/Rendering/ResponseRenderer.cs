using Spectre.Console;
using Spectre.Console.Rendering;

namespace Semantic.Engine.Client.Cli.Rendering;

/// <summary>
/// Builds the renderable of an agent response: prose as text and fenced code as language-colored panels.
/// </summary>
public static class ResponseRenderer
{
    /// <summary>
    /// Builds the panel that wraps a response.
    /// </summary>
    /// <param name="text">The accumulated response text.</param>
    /// <returns>A renderable that can be printed or placed into a live display.</returns>
    public static IRenderable Render(string text)
    {
        List<IRenderable> parts = [];
        foreach (MarkdownSegment segment in MarkdownSegmenter.Split(text))
        {
            parts.Add(segment.Kind == MarkdownSegmentKind.Code ? RenderCode(segment) : new Markup(Markup.Escape(segment.Content)));
        }

        if (parts.Count == 0)
        {
            parts.Add(new Markup(" "));
        }

        return new Panel(new Rows(parts))
            .Header("[cyan] Agent [/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Expand();
    }

    private static IRenderable RenderCode(MarkdownSegment segment)
    {
        string body = CSharpHighlighter.IsCSharp(segment.Language)
            ? CSharpHighlighter.ToMarkup(segment.Content)
            : $"[grey85]{Markup.Escape(segment.Content)}[/]";

        string header = segment.Language.Length > 0 ? Markup.Escape(segment.Language) : "code";
        return new Panel(new Markup(body.Length > 0 ? body : " "))
            .Header($"[grey] {header} [/]")
            .Border(BoxBorder.Square)
            .BorderColor(Color.Grey)
            .Expand();
    }
}
