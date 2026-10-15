namespace Semantic.Engine.Contracts.Events;

/// <summary>
/// Reports that an <see cref="Commands.IndexTextCommand"/> was fully embedded and stored.
/// </summary>
public sealed record TextIndexedEvent
{
    /// <summary>Gets the identifier of the session the memory belongs to.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the origin label of the indexed text.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Gets the number of chunks stored in the vector table.</summary>
    public int ChunkCount { get; init; }
}
