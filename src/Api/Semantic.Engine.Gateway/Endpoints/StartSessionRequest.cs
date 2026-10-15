namespace Semantic.Engine.Gateway.Endpoints;

/// <summary>
/// Request body of <c>POST /api/agent/session/start</c>.
/// </summary>
public sealed class StartSessionRequest
{
    /// <summary>Gets the first prompt of the session. Required; must not be blank.</summary>
    public string Prompt { get; init; } = string.Empty;
}
