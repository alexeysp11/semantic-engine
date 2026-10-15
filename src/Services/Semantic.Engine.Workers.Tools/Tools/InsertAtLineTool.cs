using System.Text.Json;
using System.Text.RegularExpressions;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Workers.Tools.Workspace;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// Atomically inserts text after a given line of a workspace file.
/// </summary>
public sealed partial class InsertAtLineTool(WorkspacePathResolver resolver) : ITool
{
    /// <inheritdoc />
    public string Name => ToolNames.InsertAtLine;

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(Guid sessionId, JsonElement arguments, CancellationToken cancellationToken)
    {
        string path = resolver.Resolve(sessionId, ToolArguments.GetRequiredString(arguments, "path"));
        int line = ToolArguments.GetRequiredInt(arguments, "line");
        string text = ToolArguments.GetRequiredString(arguments, "text");

        if (!File.Exists(path))
        {
            throw new ToolExecutionException("The file does not exist.");
        }

        string content = await File.ReadAllTextAsync(path, cancellationToken);
        string patched = InsertAfterLine(content, line, text);
        await AtomicFile.WriteAsync(path, patched, cancellationToken);

        return new ToolResult(true, $"Inserted text after line {line} of {resolver.ToRelative(sessionId, path)}.");
    }

    /// <summary>
    /// Inserts text after a 1-based line while preserving the line endings of the content.
    /// </summary>
    /// <param name="content">The original file content.</param>
    /// <param name="line">The line after which the text is inserted; 0 inserts at the start.</param>
    /// <param name="text">The text to insert, possibly spanning several lines.</param>
    /// <returns>The patched content.</returns>
    /// <exception cref="ToolExecutionException">The line is outside the file.</exception>
    internal static string InsertAfterLine(string content, int line, string text)
    {
        string eol = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        bool trailingEol = content.Length == 0 || content.EndsWith(eol, StringComparison.Ordinal);

        List<string> lines = [.. content.Split(eol)];
        if (trailingEol)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (line < 0 || line > lines.Count)
        {
            throw new ToolExecutionException($"The line {line} is outside the file, which has {lines.Count} lines.");
        }

        lines.InsertRange(line, LineEndings().Split(text));
        string result = string.Join(eol, lines);
        return trailingEol ? result + eol : result;
    }

    [GeneratedRegex("\r?\n")]
    private static partial Regex LineEndings();
}
