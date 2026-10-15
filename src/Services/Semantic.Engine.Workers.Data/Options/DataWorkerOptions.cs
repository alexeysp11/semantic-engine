using System.ComponentModel.DataAnnotations;

namespace Semantic.Engine.Workers.Data.Options;

/// <summary>
/// Settings of the embedding pipeline. Bound from the <c>DataWorker</c> configuration section and validated on startup.
/// </summary>
public sealed class DataWorkerOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "DataWorker";

    /// <summary>
    /// The dimensionality of the vectors produced by the embedding model.
    /// It is a property of the database schema (the <c>vector(384)</c> column) and must match <see cref="EmbeddingModelId"/>.
    /// </summary>
    public const int EmbeddingDimensions = 384;

    /// <summary>Gets or sets the Ollama endpoint URI.</summary>
    [Required]
    [Url]
    public string OllamaEndpoint { get; set; } = null!;

    /// <summary>Gets or sets the identifier of the embedding model.</summary>
    [Required]
    public string EmbeddingModelId { get; set; } = null!;

    /// <summary>Gets or sets the maximum chunk length in characters.</summary>
    [Range(50, 100000)]
    public int ChunkSizeCharacters { get; set; }

    /// <summary>Gets or sets the number of fragments returned by a memory search when the model gives no <c>top_k</c>.</summary>
    [Range(1, 100)]
    public int SearchDefaultTopK { get; set; }

    /// <summary>Gets or sets the largest number of fragments a memory search may return.</summary>
    [Range(1, 100)]
    public int SearchMaxTopK { get; set; }

    /// <summary>Gets or sets the maximum number of characters of a memory search result.</summary>
    [Range(100, 1000000)]
    public int SearchMaxOutputCharacters { get; set; }

    /// <summary>Gets or sets the number of trailing characters repeated at the start of the next chunk.</summary>
    [Range(0, 50000)]
    public int ChunkOverlapCharacters { get; set; }
}
