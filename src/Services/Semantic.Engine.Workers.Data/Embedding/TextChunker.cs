using System.Text;

namespace Semantic.Engine.Workers.Data.Embedding;

/// <summary>
/// Splits text into overlapping chunks that fit the input window of the embedding model.
/// </summary>
public static class TextChunker
{
    /// <summary>
    /// Splits text into chunks, preferring paragraph and line boundaries.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="chunkSize">The maximum chunk length in characters. Must be positive.</param>
    /// <param name="overlap">The number of trailing characters repeated at the start of the next chunk. Must be smaller than <paramref name="chunkSize"/>.</param>
    /// <returns>Non-empty chunks in document order; an empty list for blank input.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The size or the overlap is out of range.</exception>
    public static IReadOnlyList<string> Split(string text, int chunkSize, int overlap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfNegative(overlap);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overlap, chunkSize);

        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        List<string> chunks = [];
        int start = 0;
        while (start < normalized.Length)
        {
            int length = Math.Min(chunkSize, normalized.Length - start);
            int end = start + length;

            if (end < normalized.Length)
            {
                end = FindBoundary(normalized, start, end);
            }

            string chunk = normalized[start..end].Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            if (end >= normalized.Length)
            {
                break;
            }

            // Step back by the overlap but always make progress
            start = Math.Max(end - overlap, start + 1);
        }

        return chunks;
    }

    private static int FindBoundary(string text, int start, int end)
    {
        int minimum = start + ((end - start) / 2);
        foreach (string separator in new[] { "\n\n", "\n", ". ", " " })
        {
            int index = text.LastIndexOf(separator, end - 1, end - minimum, StringComparison.Ordinal);
            if (index >= minimum)
            {
                return index + separator.Length;
            }
        }

        return end;
    }
}
