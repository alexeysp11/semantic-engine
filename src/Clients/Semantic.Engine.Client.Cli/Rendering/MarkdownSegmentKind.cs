namespace Semantic.Engine.Client.Cli.Rendering;

/// <summary>
/// The kinds of response parts.
/// </summary>
public enum MarkdownSegmentKind
{
    /// <summary>Plain prose.</summary>
    Text = 0,

    /// <summary>A fenced code block.</summary>
    Code = 1
}
