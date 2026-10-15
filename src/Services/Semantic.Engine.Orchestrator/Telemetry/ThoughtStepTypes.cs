namespace Semantic.Engine.Orchestrator.Telemetry;

/// <summary>
/// Values of the <c>step_type</c> column of the thought log.
/// </summary>
public static class ThoughtStepTypes
{
    /// <summary>The model produced a text response.</summary>
    public const string AssistantMessage = "assistant_message";

    /// <summary>The model requested a tool call that passed send-safety validation.</summary>
    public const string ToolCall = "tool_call";

    /// <summary>The model requested a tool call that send-safety validation rejected.</summary>
    public const string ToolRejected = "tool_rejected";

    /// <summary>A tool finished and returned its output to the agent.</summary>
    public const string ToolResult = "tool_result";

    /// <summary>The history was condensed into a checkpoint message.</summary>
    public const string SummaryCheckpoint = "summary_checkpoint";

    /// <summary>A reasoning turn failed.</summary>
    public const string TurnFailed = "turn_failed";
}
