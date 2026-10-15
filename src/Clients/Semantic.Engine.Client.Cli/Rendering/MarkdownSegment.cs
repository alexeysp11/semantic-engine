namespace Semantic.Engine.Client.Cli.Rendering;

/// <summary>
/// A part of a model response.
/// </summary>
/// <param name="Kind">Whether the part is prose or code.</param>
/// <param name="Content">The text of the part without fences.</param>
/// <param name="Language">The lower-case language tag of a code part; empty for prose or untagged code.</param>
public sealed record MarkdownSegment(MarkdownSegmentKind Kind, string Content, string Language);
