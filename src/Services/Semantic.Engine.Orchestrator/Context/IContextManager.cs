using Microsoft.Extensions.AI;

namespace Semantic.Engine.Orchestrator.Context;

/// <summary>
/// Keeps the conversation history inside the context window of the model.
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// Estimates the number of tokens occupied by a history.
    /// </summary>
    /// <param name="messages">The messages to measure.</param>
    /// <returns>The estimated token count.</returns>
    int EstimateTokens(IReadOnlyList<ChatMessage> messages);

    /// <summary>
    /// Condenses the old part of the history into a single checkpoint message when a threshold is breached.
    /// </summary>
    /// <param name="sessionId">The identifier of the session that owns the history.</param>
    /// <param name="history">The current conversation history without the system prompt.</param>
    /// <param name="cancellationToken">A token that cancels the summarization request.</param>
    /// <returns>The original history when no threshold is breached; otherwise the pruned history.</returns>
    Task<IReadOnlyList<ChatMessage>> PruneAsync(
        Guid sessionId,
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken);
}
