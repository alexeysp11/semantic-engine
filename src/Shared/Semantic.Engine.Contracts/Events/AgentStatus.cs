namespace Semantic.Engine.Contracts.Events;

/// <summary>
/// Describes what the agent is doing from the client point of view.
/// </summary>
public enum AgentStatus
{
    /// <summary>The agent waits for the next user prompt.</summary>
    Idle = 0,

    /// <summary>The model is generating a response.</summary>
    Thinking = 1,

    /// <summary>A tool is running inside the Tool Worker.</summary>
    ExecutingTool = 2,

    /// <summary>The model history is being condensed into a checkpoint message.</summary>
    Summarizing = 3,

    /// <summary>The current turn failed; the session stays usable.</summary>
    Failed = 4
}
