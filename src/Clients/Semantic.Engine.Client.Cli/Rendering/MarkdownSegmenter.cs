namespace Semantic.Engine.Client.Cli.Rendering;

/// <summary>
/// Splits a streamed model response into prose and fenced code segments.
/// </summary>
/// <remarks>
/// The input may end in the middle of a fence while the response is still streaming; an unterminated fence is
/// returned as an incomplete code segment so that code is highlighted as soon as it starts to arrive.
/// </remarks>
public static class MarkdownSegmenter
{
    private const string Fence = "```";

    /// <summary>
    /// Splits text into segments in document order.
    /// </summary>
    /// <param name="text">The accumulated response text.</param>
    /// <returns>The segments; empty segments are omitted.</returns>
    public static IReadOnlyList<MarkdownSegment> Split(string text)
    {
        List<MarkdownSegment> segments = [];
        int position = 0;

        while (position < text.Length)
        {
            int fenceStart = text.IndexOf(Fence, position, StringComparison.Ordinal);
            if (fenceStart < 0)
            {
                AddText(segments, text[position..]);
                break;
            }

            AddText(segments, text[position..fenceStart]);

            int headerEnd = text.IndexOf('\n', fenceStart + Fence.Length);
            if (headerEnd < 0)
            {
                // The language header is still streaming
                break;
            }

            string language = text[(fenceStart + Fence.Length)..headerEnd].Trim().ToLowerInvariant();
            int codeStart = headerEnd + 1;
            int fenceEnd = text.IndexOf(Fence, codeStart, StringComparison.Ordinal);

            if (fenceEnd < 0)
            {
                segments.Add(new MarkdownSegment(MarkdownSegmentKind.Code, text[codeStart..].TrimEnd('\n'), language));
                break;
            }

            segments.Add(new MarkdownSegment(MarkdownSegmentKind.Code, text[codeStart..fenceEnd].TrimEnd('\n'), language));
            position = fenceEnd + Fence.Length;
        }

        return segments;
    }

    private static void AddText(List<MarkdownSegment> segments, string text)
    {
        string trimmed = text.Trim('\n', '\r');
        if (trimmed.Trim().Length > 0)
        {
            segments.Add(new MarkdownSegment(MarkdownSegmentKind.Text, trimmed, string.Empty));
        }
    }
}
