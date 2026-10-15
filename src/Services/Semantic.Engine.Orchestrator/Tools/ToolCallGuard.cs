using System.Text.Json;

namespace Semantic.Engine.Orchestrator.Tools;

/// <summary>
/// Send-safety middleware that validates a tool call before it is published to the bus.
/// </summary>
/// <remarks>
/// The guard rejects unknown tools, malformed argument JSON, missing required arguments and path arguments
/// that attempt to leave the workspace. The Tool Worker repeats the path validation as the authoritative check.
/// </remarks>
public static class ToolCallGuard
{
    private static readonly string[] PathArguments = ["path", "project_path"];

    /// <summary>
    /// Validates a tool call.
    /// </summary>
    /// <param name="toolName">The tool requested by the model.</param>
    /// <param name="argumentsJson">The arguments serialized as a JSON object.</param>
    /// <returns>A validation result describing the rejection reason when the call is not allowed.</returns>
    public static ToolCallValidationResult Validate(string toolName, string argumentsJson)
    {
        ToolDefinition? definition = ToolCatalog.Find(toolName);
        if (definition is null)
        {
            return ToolCallValidationResult.Reject(
                $"Unknown tool '{toolName}'. Available tools: {string.Join(", ", ToolCatalog.Names)}.");
        }

        JsonElement arguments;
        try
        {
            using JsonDocument document = JsonDocument.Parse(argumentsJson);
            arguments = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ToolCallValidationResult.Reject("Tool arguments are not valid JSON.");
        }

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return ToolCallValidationResult.Reject("Tool arguments must be a JSON object.");
        }

        foreach (string required in definition.RequiredArguments)
        {
            if (!arguments.TryGetProperty(required, out _))
            {
                return ToolCallValidationResult.Reject($"Tool '{toolName}' requires the argument '{required}'.");
            }
        }

        foreach (string pathArgument in PathArguments)
        {
            if (arguments.TryGetProperty(pathArgument, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && IsUnsafePath(value.GetString()!))
            {
                return ToolCallValidationResult.Reject(
                    $"The path '{value.GetString()}' leaves the session workspace. Use a relative path inside the workspace.");
            }
        }

        return ToolCallValidationResult.Accept();
    }

    private static bool IsUnsafePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return true;
        }

        return path.Split('/', '\\').Contains("..");
    }
}

/// <summary>
/// The outcome of <see cref="ToolCallGuard.Validate"/>.
/// </summary>
public sealed class ToolCallValidationResult
{
    private ToolCallValidationResult()
    {
    }

    /// <summary>Gets a value indicating whether the call may be executed.</summary>
    public bool IsAllowed { get; private init; }

    /// <summary>Gets the rejection reason that is returned to the model; <see langword="null"/> when allowed.</summary>
    public string? RejectionReason { get; private init; }

    /// <summary>Creates a result that allows the call.</summary>
    public static ToolCallValidationResult Accept() => new() { IsAllowed = true };

    /// <summary>Creates a result that rejects the call.</summary>
    /// <param name="reason">The reason shown to the model.</param>
    public static ToolCallValidationResult Reject(string reason) => new() { IsAllowed = false, RejectionReason = reason };
}
