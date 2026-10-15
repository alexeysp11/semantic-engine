using System.Text.Json;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Workers.Tools.Dotnet;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// Compiles the session workspace and returns the compiler errors to the agent.
/// </summary>
/// <remarks>This tool is the compilation critic that drives the self-correction loop of the agent.</remarks>
public sealed class DotnetBuildTool(DotnetWorkspaceExecutor executor) : ITool
{
    /// <inheritdoc />
    public string Name => ToolNames.DotnetBuild;

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(Guid sessionId, JsonElement arguments, CancellationToken cancellationToken)
    {
        return executor.BuildAsync(sessionId, ToolArguments.GetOptionalString(arguments, "project_path"), cancellationToken);
    }
}
