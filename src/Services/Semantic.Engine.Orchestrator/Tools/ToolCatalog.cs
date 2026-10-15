using System.Text.Json;
using Microsoft.Extensions.AI;
using Semantic.Engine.Contracts.Tools;

namespace Semantic.Engine.Orchestrator.Tools;

/// <summary>
/// Declares the tools the model may call together with their JSON argument schemas.
/// </summary>
public static class ToolCatalog
{
    private static readonly ToolDefinition[] Definitions =
    [
        new(
            ToolNames.ListFiles,
            "Lists files and directories under a directory of the session workspace.",
            """{"type":"object","properties":{"path":{"type":"string","description":"Relative directory path. Defaults to the workspace root."}},"required":[]}"""),
        new(
            ToolNames.ReadFile,
            "Reads a UTF-8 text file from the session workspace.",
            """{"type":"object","properties":{"path":{"type":"string","description":"Relative file path."}},"required":["path"]}"""),
        new(
            ToolNames.WriteFile,
            "Creates or atomically overwrites a file in the session workspace.",
            """{"type":"object","properties":{"path":{"type":"string","description":"Relative file path."},"content":{"type":"string","description":"Complete file content."}},"required":["path","content"]}"""),
        new(
            ToolNames.PatchFile,
            "Atomically replaces one exact text fragment of a workspace file. Fails when the fragment is missing or ambiguous.",
            """{"type":"object","properties":{"path":{"type":"string"},"old_text":{"type":"string","description":"Exact text to replace; must occur once."},"new_text":{"type":"string","description":"Replacement text."}},"required":["path","old_text","new_text"]}"""),
        new(
            ToolNames.InsertAtLine,
            "Atomically inserts text after a 1-based line of a workspace file. Line 0 inserts at the start.",
            """{"type":"object","properties":{"path":{"type":"string"},"line":{"type":"integer","description":"1-based line after which the text is inserted."},"text":{"type":"string"}},"required":["path","line","text"]}"""),
        new(
            ToolNames.HttpRequest,
            "Sends an HTTP request to an external API and returns the status code and response body.",
            """{"type":"object","properties":{"method":{"type":"string","enum":["GET","POST","PUT","DELETE"]},"url":{"type":"string","description":"Absolute http or https URL."},"body":{"type":"string","description":"Optional request body."}},"required":["method","url"]}"""),
        new(
            ToolNames.SearchMemory,
            "Semantic search over the memories of this session, such as the task description and conversation checkpoints. Returns the most similar fragments.",
            """{"type":"object","properties":{"query":{"type":"string","description":"Natural language description of what to look for."},"top_k":{"type":"integer","description":"Number of fragments to return."}},"required":["query"]}"""),
        new(
            ToolNames.DotnetBuild,
            "Runs dotnet build on a copy of the session workspace and returns the compiler errors.",
            """{"type":"object","properties":{"project_path":{"type":"string","description":"Optional relative path of a project or solution."}},"required":[]}"""),
        new(
            ToolNames.DotnetTest,
            "Runs dotnet test on a copy of the session workspace and returns the failures.",
            """{"type":"object","properties":{"project_path":{"type":"string","description":"Optional relative path of a project or solution."}},"required":[]}""")
    ];

    /// <summary>
    /// Gets the tool declarations in the form expected by <see cref="ChatOptions.Tools"/>.
    /// </summary>
    public static IList<AITool> CreateDeclarations()
    {
        List<AITool> declarations = [];
        foreach (ToolDefinition definition in Definitions)
        {
            JsonElement schema = JsonDocument.Parse(definition.SchemaJson).RootElement.Clone();
            declarations.Add(AIFunctionFactory.CreateDeclaration(definition.Name, definition.Description, schema));
        }

        return declarations;
    }

    /// <summary>
    /// Finds the definition of a tool by name.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <returns>The definition, or <see langword="null"/> when the tool is unknown.</returns>
    public static ToolDefinition? Find(string name)
    {
        return Definitions.FirstOrDefault(definition => definition.Name == name);
    }

    /// <summary>
    /// Gets the names of all declared tools.
    /// </summary>
    public static IReadOnlyList<string> Names => Definitions.Select(definition => definition.Name).ToList();
}
