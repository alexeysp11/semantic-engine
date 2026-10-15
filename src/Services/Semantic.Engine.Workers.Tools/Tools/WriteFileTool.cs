using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Workers.Tools.Options;
using Semantic.Engine.Workers.Tools.Workspace;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// Creates or atomically overwrites a file in the session workspace.
/// </summary>
public sealed class WriteFileTool(WorkspacePathResolver resolver, IOptions<ToolsWorkerOptions> options) : ITool
{
    /// <inheritdoc />
    public string Name => ToolNames.WriteFile;

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(Guid sessionId, JsonElement arguments, CancellationToken cancellationToken)
    {
        string relativePath = ToolArguments.GetRequiredString(arguments, "path");
        string content = ToolArguments.GetRequiredString(arguments, "content");
        string path = resolver.Resolve(sessionId, relativePath);

        if (Directory.Exists(path))
        {
            throw new ToolExecutionException("The path is a directory.");
        }

        int size = Encoding.UTF8.GetByteCount(content);
        if (size > options.Value.MaxFileSizeBytes)
        {
            throw new ToolExecutionException(
                $"The content is {size} bytes, which exceeds the limit of {options.Value.MaxFileSizeBytes} bytes.");
        }

        await AtomicFile.WriteAsync(path, content, cancellationToken);
        return new ToolResult(true, $"Wrote {size} bytes to {resolver.ToRelative(sessionId, path)}.");
    }
}
