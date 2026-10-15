using Microsoft.Extensions.Options;
using Semantic.Engine.Orchestrator.Options;

namespace Semantic.Engine.Orchestrator.Agent;

/// <summary>
/// Loads system prompts from the files configured in <see cref="AiSettingsOptions"/>.
/// </summary>
/// <remarks>
/// Relative paths are resolved against the application base directory. Files are read once at first use,
/// so a missing or empty prompt file fails fast instead of silently running the model without constraints.
/// </remarks>
public sealed class FilePromptProvider(IOptions<AiSettingsOptions> options) : IPromptProvider
{
    private readonly Lazy<string> _agentPrompt = new(() => Load(options.Value.AgentPromptPath));
    private readonly Lazy<string> _summarizerPrompt = new(() => Load(options.Value.SummarizerPromptPath));

    /// <inheritdoc />
    public string AgentPrompt => _agentPrompt.Value;

    /// <inheritdoc />
    public string SummarizerPrompt => _summarizerPrompt.Value;

    private static string Load(string configuredPath)
    {
        string path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The system prompt file '{configuredPath}' does not exist.", path);
        }

        string prompt = File.ReadAllText(path).Trim();
        if (prompt.Length == 0)
        {
            throw new InvalidOperationException($"The system prompt file '{configuredPath}' is empty.");
        }

        return prompt;
    }
}
