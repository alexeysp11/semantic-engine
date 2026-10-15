using Microsoft.Extensions.Options;
using Semantic.Engine.Workers.Tools.Options;

namespace Semantic.Engine.Workers.Tools.Workspace;

/// <summary>
/// Maps session-relative paths requested by the model to absolute paths and guarantees that they stay inside the session workspace.
/// </summary>
/// <remarks>
/// A path is rejected when it is rooted, escapes the workspace through <c>..</c> segments or crosses a symbolic link
/// or junction, which could otherwise redirect writes outside the workspace.
/// </remarks>
public sealed class WorkspacePathResolver(IOptions<ToolsWorkerOptions> options)
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Gets the absolute workspace directory of a session and creates it when missing.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The absolute directory path without a trailing separator.</returns>
    public string GetSessionRoot(Guid sessionId)
    {
        string root = Path.GetFullPath(Path.Combine(options.Value.WorkspaceRoot, sessionId.ToString("N")));
        Directory.CreateDirectory(root);
        return root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Resolves a relative path inside the session workspace.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="relativePath">The path requested by the model. Empty input resolves to the workspace root.</param>
    /// <returns>The absolute path.</returns>
    /// <exception cref="ToolExecutionException">The path is rooted, leaves the workspace or crosses a reparse point.</exception>
    public string Resolve(Guid sessionId, string? relativePath)
    {
        string root = GetSessionRoot(sessionId);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return root;
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new ToolExecutionException($"The path '{relativePath}' is absolute. Use a path relative to the workspace root.");
        }

        string fullPath = Path.GetFullPath(Path.Combine(root, relativePath))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        bool isRoot = string.Equals(fullPath, root, PathComparison);
        bool isInside = fullPath.StartsWith(root + Path.DirectorySeparatorChar, PathComparison);
        if (!isRoot && !isInside)
        {
            throw new ToolExecutionException($"The path '{relativePath}' leaves the session workspace.");
        }

        EnsureNoReparsePoints(root, fullPath, relativePath);
        return fullPath;
    }

    /// <summary>
    /// Converts an absolute workspace path back to a forward-slash relative path.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="fullPath">An absolute path inside the workspace.</param>
    /// <returns>The relative path; <c>.</c> for the workspace root.</returns>
    public string ToRelative(Guid sessionId, string fullPath)
    {
        string relative = Path.GetRelativePath(GetSessionRoot(sessionId), fullPath);
        return relative.Replace('\\', '/');
    }

    private static void EnsureNoReparsePoints(string root, string fullPath, string requestedPath)
    {
        string current = root;
        string remainder = Path.GetRelativePath(root, fullPath);
        if (remainder == ".")
        {
            return;
        }

        foreach (string segment in remainder.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                return;
            }

            if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new ToolExecutionException($"The path '{requestedPath}' crosses a symbolic link, which is not allowed.");
            }
        }
    }
}
