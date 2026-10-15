using System.ComponentModel.DataAnnotations;

namespace Semantic.Engine.Client.Cli.Options;

/// <summary>
/// Connection settings of the Gateway. Bound from the <c>Gateway</c> configuration section.
/// </summary>
public sealed class GatewayOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Gateway";

    /// <summary>Gets or sets the base address of the Gateway.</summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = null!;

    /// <summary>Gets or sets the API key sent in the <c>X-Api-Key</c> header. Leave empty when the Gateway has no key configured.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the timeout of non-streaming requests, in seconds.</summary>
    [Range(1, 600)]
    public int RequestTimeoutSeconds { get; set; }
}
