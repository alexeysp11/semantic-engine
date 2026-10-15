namespace Semantic.Engine.Contracts.Commands;

/// <summary>
/// Requests chunking, embedding and indexing of a text fragment into the vector store.
/// </summary>
public sealed record IndexTextCommand
{
    /// <summary>Gets the identifier of the session the memory belongs to.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets a short label describing the origin of the text, for example <c>task-summary</c>.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Gets the text to index. Must not be empty.</summary>
    public string Text { get; init; } = string.Empty;
}
