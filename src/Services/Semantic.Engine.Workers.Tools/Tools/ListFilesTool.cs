using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Workers.Tools.Options;
using Semantic.Engine.Workers.Tools.Workspace;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// Lists the entries of a workspace directory.
/// </summary>
public sealed class ListFilesTool(WorkspacePathResolver resolver, IOptions<ToolsWorkerOptions> options) : ITool
{
    private static readonly string[] HiddenDirectories = ["bin", "obj", ".git"];

    /// <inheritdoc />
    public string Name => ToolNames.ListFiles;

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(Guid sessionId, JsonElement arguments, CancellationToken cancellationToken)
    {
        string directory = resolver.Resolve(sessionId, ToolArguments.GetOptionalString(arguments, "path"));
        if (!Directory.Exists(directory))
        {
            throw new ToolExecutionException("The directory does not exist.");
        }

        int maxEntries = options.Value.MaxListEntries;
        List<string> entries = Directory
            .EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
            .Where(entry => !IsHidden(directory, entry))
            .Select(entry => resolver.ToRelative(sessionId, entry) + (Directory.Exists(entry) ? "/" : string.Empty))
            .Order(StringComparer.Ordinal)
            .Take(maxEntries + 1)
            .ToList();

        if (entries.Count == 0)
        {
            return Task.FromResult(new ToolResult(true, "The directory is empty."));
        }

        StringBuilder output = new();
        foreach (string entry in entries.Take(maxEntries))
        {
            output.AppendLine(entry);
        }

        if (entries.Count > maxEntries)
        {
            output.AppendLine($"... truncated after {maxEntries} entries.");
        }

        return Task.FromResult(new ToolResult(true, output.ToString().TrimEnd()));
    }

    private static bool IsHidden(string root, string entry)
    {
        string[] segments = Path.GetRelativePath(root, entry).Split(Path.DirectorySeparatorChar);
        return segments.Any(segment => HiddenDirectories.Contains(segment) || segment.EndsWith(".tmp", StringComparison.Ordinal));
    }
}
