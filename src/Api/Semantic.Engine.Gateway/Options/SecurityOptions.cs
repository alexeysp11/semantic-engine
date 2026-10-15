namespace Semantic.Engine.Gateway.Options;

/// <summary>
/// Access control settings. Bound from the <c>Security</c> configuration section.
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Security";

    /// <summary>The name of the HTTP header that carries the API key.</summary>
    public const string ApiKeyHeader = "X-Api-Key";

    /// <summary>
    /// Gets or sets the API key that clients must send in the <c>X-Api-Key</c> header.
    /// When empty or missing, authentication is disabled; use that only on a trusted local machine.
    /// </summary>
    public string? ApiKey { get; set; }
}
