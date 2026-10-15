using System.Text.Json;
using Microsoft.Extensions.Options;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Workers.Tools.Options;
using Semantic.Engine.Workers.Tools.Workspace;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// Reads a UTF-8 text file from the session workspace.
/// </summary>
public sealed class ReadFileTool(WorkspacePathResolver resolver, IOptions<ToolsWorkerOptions> options) : ITool
{
    /// <inheritdoc />
    public string Name => ToolNames.ReadFile;

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(Guid sessionId, JsonElement arguments, CancellationToken cancellationToken)
    {
        string path = resolver.Resolve(sessionId, ToolArguments.GetRequiredString(arguments, "path"));
        if (!File.Exists(path))
        {
            throw new ToolExecutionException("The file does not exist.");
        }

        long length = new FileInfo(path).Length;
        if (length > options.Value.MaxFileSizeBytes)
        {
            throw new ToolExecutionException(
                $"The file is {length} bytes, which exceeds the limit of {options.Value.MaxFileSizeBytes} bytes.");
        }

        string content = await File.ReadAllTextAsync(path, cancellationToken);
        return new ToolResult(true, content);
    }
}
