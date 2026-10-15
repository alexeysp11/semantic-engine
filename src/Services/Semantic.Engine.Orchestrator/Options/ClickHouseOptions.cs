using System.ComponentModel.DataAnnotations;

namespace Semantic.Engine.Orchestrator.Options;

/// <summary>
/// Connection settings of the ClickHouse HTTP interface that stores thought logs.
/// Bound from the <c>ClickHouse</c> configuration section and validated on startup.
/// </summary>
public sealed class ClickHouseOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ClickHouse";

    /// <summary>Gets or sets a value indicating whether thought logging is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the ClickHouse HTTP endpoint.</summary>
    [Required]
    [Url]
    public string Endpoint { get; set; } = null!;

    /// <summary>Gets or sets the database name.</summary>
    [Required]
    public string Database { get; set; } = null!;

    /// <summary>Gets or sets the user name.</summary>
    [Required]
    public string User { get; set; } = null!;

    /// <summary>Gets or sets the password.</summary>
    [Required]
    public string Password { get; set; } = null!;

    /// <summary>Gets or sets the timeout of a single insert request, in seconds.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; }
}
