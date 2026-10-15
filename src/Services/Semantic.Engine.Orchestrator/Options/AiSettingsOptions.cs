using System.ComponentModel.DataAnnotations;

namespace Semantic.Engine.Orchestrator.Options;

/// <summary>
/// Settings of the local LLM backend. Bound from the <c>AiSettings</c> configuration section.
/// All values are supplied by configuration and validated on startup.
/// </summary>
public sealed class AiSettingsOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "AiSettings";

    /// <summary>Gets or sets the Ollama endpoint URI.</summary>
    [Required]
    [Url]
    public string Endpoint { get; set; } = null!;

    /// <summary>Gets or sets the identifier of the chat model.</summary>
    [Required]
    public string ModelId { get; set; } = null!;

    /// <summary>Gets or sets the path of the agent system prompt file, relative to the application directory.</summary>
    [Required]
    public string AgentPromptPath { get; set; } = null!;

    /// <summary>Gets or sets the path of the summarizer system prompt file, relative to the application directory.</summary>
    [Required]
    public string SummarizerPromptPath { get; set; } = null!;

    /// <summary>Gets or sets the sampling temperature of the agent. Low values keep tool calls deterministic.</summary>
    [Range(0.0, 2.0)]
    public float Temperature { get; set; }

    /// <summary>Gets or sets the sampling temperature of the summarizer.</summary>
    [Range(0.0, 2.0)]
    public float SummarizerTemperature { get; set; }

    /// <summary>Gets or sets the maximum number of rejected tool calls the model may retry within one turn.</summary>
    [Range(1, 20)]
    public int MaxRejectedToolCalls { get; set; }

    /// <summary>Gets or sets the HTTP timeout of a single Ollama request, in minutes. CPU inference can be slow.</summary>
    [Range(1, 240)]
    public int RequestTimeoutMinutes { get; set; }
}
