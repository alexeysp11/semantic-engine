using System.ComponentModel.DataAnnotations;

namespace Semantic.Engine.Workers.Tools.Options;

/// <summary>
/// Limits and locations used by the Tool Worker. Bound from the <c>ToolsWorker</c> configuration section
/// and validated on startup.
/// </summary>
public sealed class ToolsWorkerOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ToolsWorker";

    /// <summary>Gets or sets the directory that contains one sub-directory per session workspace.</summary>
    [Required]
    public string WorkspaceRoot { get; set; } = null!;

    /// <summary>Gets or sets the path of the class library project template used when a workspace has no project file.</summary>
    [Required]
    public string ScaffoldProjectPath { get; set; } = null!;

    /// <summary>Gets or sets the maximum size of a file the agent may read or write, in bytes.</summary>
    [Range(1, 104857600)]
    public int MaxFileSizeBytes { get; set; }

    /// <summary>Gets or sets the maximum number of characters returned to the model by a single tool.</summary>
    [Range(100, 1000000)]
    public int MaxOutputCharacters { get; set; }

    /// <summary>Gets or sets the maximum number of entries returned by the directory listing tool.</summary>
    [Range(1, 10000)]
    public int MaxListEntries { get; set; }

    /// <summary>Gets or sets the maximum number of compiler errors returned to the model.</summary>
    [Range(1, 1000)]
    public int MaxReportedErrors { get; set; }

    /// <summary>Gets or sets the maximum number of compiler warnings returned to the model.</summary>
    [Range(0, 1000)]
    public int MaxReportedWarnings { get; set; }

    /// <summary>Gets or sets the maximum duration of <c>dotnet build</c> and <c>dotnet test</c>, in seconds.</summary>
    [Range(1, 3600)]
    public int DotnetTimeoutSeconds { get; set; }

    /// <summary>Gets or sets the timeout of an outgoing HTTP request, in seconds.</summary>
    [Range(1, 600)]
    public int HttpTimeoutSeconds { get; set; }

    /// <summary>Gets or sets a value indicating whether HTTP requests to loopback and private network addresses are allowed.</summary>
    public bool AllowPrivateNetworkTargets { get; set; }
}
