using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Semantic.Engine.Workers.Tools.Options;
using Semantic.Engine.Workers.Tools.Tools;
using Semantic.Engine.Workers.Tools.Workspace;

namespace Semantic.Engine.Workers.Tools.Dotnet;

/// <summary>
/// Runs <c>dotnet build</c> or <c>dotnet test</c> against a throw-away copy of the session workspace.
/// </summary>
/// <remarks>
/// The copy keeps build artifacts out of the workspace and isolates concurrent sessions. When the workspace contains
/// no project file, a minimal class library project is generated so that loose <c>.cs</c> files can be compiled.
/// Compiler diagnostics are extracted and returned verbatim so that the model can self-correct.
/// </remarks>
public sealed partial class DotnetWorkspaceExecutor(
    WorkspacePathResolver resolver,
    DotnetCommandRunner runner,
    IOptions<ToolsWorkerOptions> options)
{
    private const string ScaffoldProjectName = "Workspace.csproj";

    private static readonly string[] ExcludedDirectories = ["bin", "obj", ".git"];

    /// <summary>
    /// Builds the workspace.
    /// </summary>
    /// <param name="sessionId">The session whose workspace is built.</param>
    /// <param name="projectPath">An optional relative path of a project or solution.</param>
    /// <param name="cancellationToken">A token that cancels the build.</param>
    /// <returns>A failed result containing the compiler errors, or a succeeded result with the warning count.</returns>
    public Task<ToolResult> BuildAsync(Guid sessionId, string? projectPath, CancellationToken cancellationToken)
    {
        return RunAsync(sessionId, "build", projectPath, cancellationToken);
    }

    /// <summary>
    /// Runs the tests of the workspace.
    /// </summary>
    /// <param name="sessionId">The session whose workspace is tested.</param>
    /// <param name="projectPath">An optional relative path of a project or solution.</param>
    /// <param name="cancellationToken">A token that cancels the test run.</param>
    /// <returns>The test summary and failures.</returns>
    public Task<ToolResult> TestAsync(Guid sessionId, string? projectPath, CancellationToken cancellationToken)
    {
        return RunAsync(sessionId, "test", projectPath, cancellationToken);
    }

    private async Task<ToolResult> RunAsync(Guid sessionId, string verb, string? projectPath, CancellationToken cancellationToken)
    {
        string source = resolver.GetSessionRoot(sessionId);
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "semantic-engine-build", Guid.NewGuid().ToString("N"));

        try
        {
            CopyDirectory(source, temporaryRoot);

            string? target = null;
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                string resolved = resolver.Resolve(sessionId, projectPath);
                if (!File.Exists(resolved) && !Directory.Exists(resolved))
                {
                    throw new ToolExecutionException($"The project path '{projectPath}' does not exist.");
                }

                target = Path.Combine(temporaryRoot, resolver.ToRelative(sessionId, resolved));
            }
            else if (!ContainsProject(temporaryRoot))
            {
                await File.WriteAllTextAsync(Path.Combine(temporaryRoot, ScaffoldProjectName), await LoadScaffoldAsync(cancellationToken), cancellationToken);
            }

            List<string> arguments = [verb];
            if (target is not null)
            {
                arguments.Add(target);
            }

            arguments.AddRange(["--nologo", "-v", "q", "-clp:NoSummary"]);

            DotnetCommandResult result = await runner.RunAsync(temporaryRoot, arguments, cancellationToken);
            return Format(verb, result, temporaryRoot);
        }
        finally
        {
            DeleteDirectory(temporaryRoot);
        }
    }

    private ToolResult Format(string verb, DotnetCommandResult result, string temporaryRoot)
    {
        if (result.TimedOut)
        {
            return new ToolResult(false, $"dotnet {verb} timed out after {options.Value.DotnetTimeoutSeconds} seconds.");
        }

        List<string> lines = result.Output
            .Replace(temporaryRoot + Path.DirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => ProjectSuffix().Replace(line, string.Empty))
            .ToList();

        List<string> errors = lines.Where(line => line.Contains(": error ", StringComparison.Ordinal)).Distinct().ToList();
        List<string> warnings = lines.Where(line => line.Contains(": warning ", StringComparison.Ordinal)).Distinct().ToList();

        StringBuilder output = new();
        if (verb == "build")
        {
            if (result.ExitCode == 0)
            {
                output.AppendLine($"Build succeeded. 0 errors, {warnings.Count} warnings.");
                AppendLimited(output, warnings, options.Value.MaxReportedWarnings);
            }
            else
            {
                output.AppendLine($"Build FAILED with {errors.Count} error(s).");
                if (errors.Count > 0)
                {
                    AppendLimited(output, errors, options.Value.MaxReportedErrors);
                }
                else
                {
                    output.AppendLine(string.Join('\n', lines));
                }
            }
        }
        else
        {
            output.AppendLine(result.ExitCode == 0 ? "Tests passed." : "Tests FAILED.");
            output.AppendLine(string.Join('\n', lines));
        }

        return new ToolResult(result.ExitCode == 0, Truncate(output.ToString().TrimEnd()));
    }

    private async Task<string> LoadScaffoldAsync(CancellationToken cancellationToken)
    {
        string path = Path.IsPathRooted(options.Value.ScaffoldProjectPath)
            ? options.Value.ScaffoldProjectPath
            : Path.Combine(AppContext.BaseDirectory, options.Value.ScaffoldProjectPath);
        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private string Truncate(string text)
    {
        int limit = options.Value.MaxOutputCharacters;
        return text.Length <= limit ? text : string.Concat(text.AsSpan(0, limit), "\n...[truncated]");
    }

    private static void AppendLimited(StringBuilder output, List<string> lines, int limit)
    {
        foreach (string line in lines.Take(limit))
        {
            output.AppendLine(line);
        }

        if (lines.Count > limit)
        {
            output.AppendLine($"... and {lines.Count - limit} more.");
        }
    }

    private static bool ContainsProject(string directory)
    {
        return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Any(file => file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                         || file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                         || file.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.EnumerateFiles(source))
        {
            if (!file.EndsWith(".tmp", StringComparison.Ordinal))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            string name = Path.GetFileName(directory);
            if (!ExcludedDirectories.Contains(name))
            {
                CopyDirectory(directory, Path.Combine(destination, name));
            }
        }
    }

    private static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A lingering compiler server can hold files briefly; the OS temp cleanup removes the leftovers
        }
        catch (UnauthorizedAccessException)
        {
            // See above
        }
    }

    [GeneratedRegex(@"\s\[[^\]]+\.(csproj|sln|slnx)\]$")]
    private static partial Regex ProjectSuffix();
}
