using System.Text;

namespace Semantic.Engine.Workers.Tools.Workspace;

/// <summary>
/// Writes files so that readers never observe a partially written file.
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// Writes UTF-8 content to a temporary sibling file and moves it over the target.
    /// </summary>
    /// <param name="path">The absolute target path. Missing parent directories are created.</param>
    /// <param name="content">The complete file content.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    public static async Task WriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, new UTF8Encoding(false), cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
