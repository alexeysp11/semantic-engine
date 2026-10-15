using System.Text.Json;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// A capability the agent can invoke through an <c>ExecuteToolCommand</c>.
/// </summary>
public interface ITool
{
    /// <summary>Gets the tool name, matching an entry of <c>ToolNames</c>.</summary>
    string Name { get; }

    /// <summary>
    /// Executes the tool.
    /// </summary>
    /// <param name="sessionId">The session whose workspace the tool operates on.</param>
    /// <param name="arguments">The arguments as a JSON object.</param>
    /// <param name="cancellationToken">A token that cancels the execution.</param>
    /// <returns>The outcome that is returned to the model.</returns>
    /// <exception cref="Workspace.ToolExecutionException">The call is invalid or failed in a way the model can correct.</exception>
    Task<ToolResult> ExecuteAsync(Guid sessionId, JsonElement arguments, CancellationToken cancellationToken);
}

/// <summary>
/// The outcome of a tool execution.
/// </summary>
/// <param name="Succeeded">Whether the tool completed without errors.</param>
/// <param name="Output">The text returned to the model.</param>
public sealed record ToolResult(bool Succeeded, string Output);
