using System.Text.Json;
using System.Text.RegularExpressions;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Workers.Tools.Workspace;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// Atomically replaces one exact text fragment of a workspace file.
/// </summary>
/// <remarks>
/// The fragment must occur exactly once. Line endings of the fragment are adapted to the line endings of the file,
/// because models usually emit <c>\n</c> even for CRLF files.
/// </remarks>
public sealed partial class PatchFileTool(WorkspacePathResolver resolver) : ITool
{
    /// <inheritdoc />
    public string Name => ToolNames.PatchFile;

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(Guid sessionId, JsonElement arguments, CancellationToken cancellationToken)
    {
        string path = resolver.Resolve(sessionId, ToolArguments.GetRequiredString(arguments, "path"));
        string oldText = ToolArguments.GetRequiredString(arguments, "old_text");
        string newText = ToolArguments.GetRequiredString(arguments, "new_text");

        if (oldText.Length == 0)
        {
            throw new ToolExecutionException("The argument 'old_text' must not be empty.");
        }

        if (!File.Exists(path))
        {
            throw new ToolExecutionException("The file does not exist.");
        }

        string content = await File.ReadAllTextAsync(path, cancellationToken);
        if (content.Contains("\r\n", StringComparison.Ordinal))
        {
            oldText = LineEndings().Replace(oldText, "\r\n");
            newText = LineEndings().Replace(newText, "\r\n");
        }

        int occurrences = CountOccurrences(content, oldText);
        if (occurrences == 0)
        {
            throw new ToolExecutionException("The fragment 'old_text' was not found. Read the file and copy the fragment exactly.");
        }

        if (occurrences > 1)
        {
            throw new ToolExecutionException(
                $"The fragment 'old_text' occurs {occurrences} times. Include more surrounding context to make it unique.");
        }

        int index = content.IndexOf(oldText, StringComparison.Ordinal);
        string patched = string.Concat(content.AsSpan(0, index), newText, content.AsSpan(index + oldText.Length));
        await AtomicFile.WriteAsync(path, patched, cancellationToken);

        return new ToolResult(true, $"Patched {resolver.ToRelative(sessionId, path)}.");
    }

    private static int CountOccurrences(string content, string fragment)
    {
        int count = 0;
        int index = 0;
        while ((index = content.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += fragment.Length;
        }

        return count;
    }

    [GeneratedRegex("\r?\n")]
    private static partial Regex LineEndings();
}
