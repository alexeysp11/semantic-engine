namespace Semantic.Engine.Orchestrator.Agent;

/// <summary>
/// Supplies the system prompts that constrain the model. Prompts are configuration, not code.
/// </summary>
public interface IPromptProvider
{
    /// <summary>Gets the system prompt of the engineering agent.</summary>
    string AgentPrompt { get; }

    /// <summary>Gets the system prompt of the history summarizer.</summary>
    string SummarizerPrompt { get; }
}
