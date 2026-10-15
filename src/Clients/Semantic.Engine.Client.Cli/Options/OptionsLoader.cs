using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace Semantic.Engine.Client.Cli.Options;

/// <summary>
/// Binds and validates a configuration section. The client has no built-in defaults; every value comes from configuration.
/// </summary>
public static class OptionsLoader
{
    /// <summary>
    /// Loads a validated options object.
    /// </summary>
    /// <typeparam name="T">The options type.</typeparam>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="sectionName">The name of the section to bind.</param>
    /// <returns>The bound options.</returns>
    /// <exception cref="InvalidOperationException">The section is missing or violates the validation attributes.</exception>
    public static T Load<T>(IConfiguration configuration, string sectionName)
        where T : class
    {
        T options = configuration.GetRequiredSection(sectionName).Get<T>()
            ?? throw new InvalidOperationException($"The configuration section '{sectionName}' is empty.");

        List<ValidationResult> errors = [];
        if (!Validator.TryValidateObject(options, new ValidationContext(options), errors, validateAllProperties: true))
        {
            string details = string.Join("; ", errors.Select(error => error.ErrorMessage));
            throw new InvalidOperationException($"The configuration section '{sectionName}' is invalid: {details}");
        }

        return options;
    }
}
