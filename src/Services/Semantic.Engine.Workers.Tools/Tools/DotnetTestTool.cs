using System.Text.Json;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Workers.Tools.Dotnet;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// Runs the tests of the session workspace and returns the failures to the agent.
/// </summary>
public sealed class DotnetTestTool(DotnetWorkspaceExecutor executor) : ITool
{
    /// <inheritdoc />
    public string Name => ToolNames.DotnetTest;

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(Guid sessionId, JsonElement arguments, CancellationToken cancellationToken)
    {
        return executor.TestAsync(sessionId, ToolArguments.GetOptionalString(arguments, "project_path"), cancellationToken);
    }
}
