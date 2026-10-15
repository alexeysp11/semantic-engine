using System.ComponentModel.DataAnnotations;

namespace Semantic.Engine.Orchestrator.Options;

/// <summary>
/// RabbitMQ connection settings. Bound from the <c>RabbitMq</c> configuration section and validated on startup.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "RabbitMq";

    /// <summary>Gets or sets the broker host name.</summary>
    [Required]
    public string Host { get; set; } = null!;

    /// <summary>Gets or sets the virtual host.</summary>
    [Required]
    public string VirtualHost { get; set; } = null!;

    /// <summary>Gets or sets the user name.</summary>
    [Required]
    public string Username { get; set; } = null!;

    /// <summary>Gets or sets the password.</summary>
    [Required]
    public string Password { get; set; } = null!;
}
