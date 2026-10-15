using System.Text.Json;
using Semantic.Engine.Workers.Tools.Workspace;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// Typed accessors for JSON tool arguments that produce model-friendly errors.
/// </summary>
public static class ToolArguments
{
    /// <summary>
    /// Reads a required string argument.
    /// </summary>
    /// <param name="arguments">The arguments object.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The string value.</returns>
    /// <exception cref="ToolExecutionException">The argument is missing or is not a string.</exception>
    public static string GetRequiredString(JsonElement arguments, string name)
    {
        return GetOptionalString(arguments, name)
            ?? throw new ToolExecutionException($"The required argument '{name}' is missing or is not a string.");
    }

    /// <summary>
    /// Reads an optional string argument.
    /// </summary>
    /// <param name="arguments">The arguments object.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The value, or <see langword="null"/> when the argument is absent or not a string.</returns>
    public static string? GetOptionalString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    /// <summary>
    /// Reads a required integer argument. Numeric strings are accepted because small models often quote numbers.
    /// </summary>
    /// <param name="arguments">The arguments object.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="ToolExecutionException">The argument is missing or is not an integer.</exception>
    public static int GetRequiredInt(JsonElement arguments, string name)
    {
        if (arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out JsonElement value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out int parsed))
            {
                return parsed;
            }
        }

        throw new ToolExecutionException($"The required argument '{name}' is missing or is not an integer.");
    }
}
