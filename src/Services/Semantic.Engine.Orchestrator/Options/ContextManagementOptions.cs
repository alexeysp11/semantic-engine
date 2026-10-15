using System.ComponentModel.DataAnnotations;

namespace Semantic.Engine.Orchestrator.Options;

/// <summary>
/// Thresholds of the context management engine. Bound from the <c>ContextManagement</c> configuration section.
/// All values are supplied by configuration and validated on startup.
/// </summary>
public sealed class ContextManagementOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ContextManagement";

    /// <summary>Gets or sets the message count above which the history is summarized.</summary>
    [Range(2, 10000)]
    public int MaxMessages { get; set; }

    /// <summary>Gets or sets the estimated token count above which the history is summarized.</summary>
    [Range(100, 1000000)]
    public int MaxEstimatedTokens { get; set; }

    /// <summary>Gets or sets the number of most recent messages that are never summarized.</summary>
    [Range(1, 1000)]
    public int KeepRecentMessages { get; set; }

    /// <summary>Gets or sets the average number of characters per token used for the estimation.</summary>
    [Range(1, 20)]
    public int CharactersPerToken { get; set; }

    /// <summary>Gets or sets the number of characters of a tool output that is kept in the summarization transcript.</summary>
    [Range(100, 100000)]
    public int MaxToolOutputCharactersInTranscript { get; set; }
}
