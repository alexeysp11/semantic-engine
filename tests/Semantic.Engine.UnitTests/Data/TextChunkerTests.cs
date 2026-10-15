using FluentAssertions;
using Semantic.Engine.Workers.Data.Embedding;
using Xunit;

namespace Semantic.Engine.UnitTests.Data;

public sealed class TextChunkerTests
{
    [Fact]
    public void Split_ShortText_ReturnsSingleChunk()
    {
        IReadOnlyList<string> chunks = TextChunker.Split("hello world", chunkSize: 100, overlap: 10);

        chunks
            .Should()
            .Equal(["hello world"], "because text shorter than the chunk size needs no splitting");
    }

    [Fact]
    public void Split_BlankText_ReturnsNoChunks()
    {
        TextChunker.Split("  \n ", chunkSize: 100, overlap: 10)
            .Should()
            .BeEmpty("because there is nothing to embed");
    }

    [Fact]
    public void Split_LongText_RespectsChunkSizeAndCoversAllContent()
    {
        string text = string.Join(' ', Enumerable.Range(0, 200).Select(index => $"word{index}"));

        IReadOnlyList<string> chunks = TextChunker.Split(text, chunkSize: 100, overlap: 20);

        chunks
            .Should()
            .OnlyContain(chunk => chunk.Length <= 100, "because every chunk must fit the embedding input window");
        chunks.Count
            .Should()
            .BeGreaterThan(1, "because the text is longer than one chunk");
        chunks[^1]
            .Should()
            .EndWith("word199", "because the tail of the document must not be dropped");
    }

    [Fact]
    public void Split_PrefersParagraphBoundaries()
    {
        string text = new string('a', 60) + "\n\n" + new string('b', 60);

        IReadOnlyList<string> chunks = TextChunker.Split(text, chunkSize: 100, overlap: 0);

        chunks[0]
            .Should()
            .Be(new string('a', 60), "because a paragraph break is the preferred split point");
    }

    [Fact]
    public void Split_OverlapNotSmallerThanSize_Throws()
    {
        Action act = () => TextChunker.Split("text", chunkSize: 10, overlap: 10);

        act
            .Should()
            .Throw<ArgumentOutOfRangeException>("because an overlap equal to the size would never make progress");
    }
}
