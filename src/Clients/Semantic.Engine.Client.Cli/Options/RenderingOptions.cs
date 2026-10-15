using System.ComponentModel.DataAnnotations;

namespace Semantic.Engine.Client.Cli.Options;

/// <summary>
/// Terminal rendering settings. Bound from the <c>Rendering</c> configuration section.
/// </summary>
public sealed class RenderingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Rendering";

    /// <summary>Gets or sets the minimum interval between redraws of the streamed response, in milliseconds.</summary>
    [Range(10, 2000)]
    public int LiveRefreshMilliseconds { get; set; }
}
