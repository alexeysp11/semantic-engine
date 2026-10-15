namespace Semantic.Engine.Gateway.Endpoints;

/// <summary>
/// Response body of <c>POST /api/agent/session/start</c>.
/// </summary>
public sealed class StartSessionResponse
{
    /// <summary>Gets the identifier of the created session. Use it to open the event stream and to send further prompts.</summary>
    public Guid SessionId { get; init; }
}
