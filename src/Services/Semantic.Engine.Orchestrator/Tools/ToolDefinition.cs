using System.Text.Json;

namespace Semantic.Engine.Orchestrator.Tools;

/// <summary>
/// Describes one tool of the <see cref="ToolCatalog"/>.
/// </summary>
/// <param name="Name">The tool name.</param>
/// <param name="Description">The description shown to the model.</param>
/// <param name="SchemaJson">The JSON schema of the tool arguments.</param>
public sealed record ToolDefinition(string Name, string Description, string SchemaJson)
{
    /// <summary>
    /// Gets the names of the arguments the schema marks as required.
    /// </summary>
    public IReadOnlyList<string> RequiredArguments
    {
        get
        {
            using JsonDocument document = JsonDocument.Parse(SchemaJson);
            return document.RootElement
                .GetProperty("required")
                .EnumerateArray()
                .Select(element => element.GetString()!)
                .ToList();
        }
    }
}
