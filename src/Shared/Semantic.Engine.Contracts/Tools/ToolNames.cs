namespace Semantic.Engine.Contracts.Tools;

/// <summary>
/// Names of the tools that the agent can call. Shared by the Orchestrator tool catalog and the Tool Worker handlers.
/// </summary>
public static class ToolNames
{
    /// <summary>Lists the files and directories located under a workspace directory.</summary>
    public const string ListFiles = "list_files";

    /// <summary>Reads a UTF-8 text file from the session workspace.</summary>
    public const string ReadFile = "read_file";

    /// <summary>Atomically creates or overwrites a file in the session workspace.</summary>
    public const string WriteFile = "write_file";

    /// <summary>Atomically replaces an exact text fragment of a workspace file.</summary>
    public const string PatchFile = "patch_file";

    /// <summary>Atomically inserts text after a given line of a workspace file.</summary>
    public const string InsertAtLine = "insert_at_line";

    /// <summary>Sends an HTTP request to an external API.</summary>
    public const string HttpRequest = "http_request";

    /// <summary>Searches the vector memories of the session; executed by the Data Worker.</summary>
    public const string SearchMemory = "search_memory";

    /// <summary>Runs <c>dotnet build</c> against a copy of the session workspace.</summary>
    public const string DotnetBuild = "dotnet_build";

    /// <summary>Runs <c>dotnet test</c> against a copy of the session workspace.</summary>
    public const string DotnetTest = "dotnet_test";
}
