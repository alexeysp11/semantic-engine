using Pgvector;

namespace Semantic.Engine.Workers.Data.Persistence;

/// <summary>
/// One embedded text chunk stored in the <c>memory_chunks</c> pgvector table.
/// </summary>
public sealed class MemoryChunk
{
    /// <summary>Gets or sets the chunk identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the session the memory belongs to.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the origin label of the text.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the zero-based position of the chunk inside the indexed text.</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Gets or sets the chunk text.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the embedding vector.</summary>
    public Vector Embedding { get; set; } = null!;

    /// <summary>Gets or sets the UTC creation time.</summary>
    public DateTime CreatedAtUtc { get; set; }
}
