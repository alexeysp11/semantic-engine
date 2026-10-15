namespace Semantic.Engine.Gateway.Endpoints;

/// <summary>
/// Request body of <c>POST /api/agent/prompt</c>.
/// </summary>
public sealed class SubmitPromptRequest
{
    /// <summary>Gets the identifier of an existing session. Required; must not be empty.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Gets the prompt to send. Required; must not be blank.</summary>
    public string Prompt { get; init; } = string.Empty;
}
