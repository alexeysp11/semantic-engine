using MassTransit;
using Semantic.Engine.Contracts.Commands;
using Semantic.Engine.Contracts.Events;
using Semantic.Engine.Contracts.Sagas;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Orchestrator.Context;

namespace Semantic.Engine.Orchestrator.StateMachine;

/// <summary>
/// Orchestrates the lifecycle of an agent session.
/// </summary>
/// <remarks>
/// <para>
/// The session moves through three states. <see cref="Active"/> means the session is open and waits for a prompt,
/// <see cref="Thinking"/> means the model generates a response, and <see cref="AwaitingTool"/> means the agent is frozen
/// until the Tool Worker reports the result of a requested tool call.
/// </para>
/// <para>
/// The saga owns the conversation history. Every reasoning turn receives the current history inside a
/// <see cref="RunAgentTurnCommand"/> and returns the updated history inside an <see cref="AgentTurnCompletedEvent"/>.
/// </para>
/// </remarks>
public sealed class AgentSessionStateMachine : MassTransitStateMachine<AgentSessionState>
{
    /// <summary>Initializes the state machine definition.</summary>
    public AgentSessionStateMachine()
    {
        InstanceState(state => state.CurrentState);

        Event(() => SessionStarted, configurator => configurator.CorrelateById(context => context.Message.SessionId));
        Event(() => PromptSubmitted, configurator =>
        {
            configurator.CorrelateById(context => context.Message.SessionId);
            configurator.OnMissingInstance(missing => missing.Discard());
        });
        Event(() => TurnCompleted, configurator => configurator.CorrelateById(context => context.Message.SessionId));
        Event(() => ToolExecuted, configurator => configurator.CorrelateById(context => context.Message.SessionId));

        Initially(
            When(SessionStarted)
                .Then(context =>
                {
                    context.Saga.ConversationHistoryJson = ConversationHistory.AppendUserMessage(
                        ConversationHistory.Empty,
                        context.Message.Prompt);
                    context.Saga.CreatedAtUtc = DateTime.UtcNow;
                    context.Saga.UpdatedAtUtc = context.Saga.CreatedAtUtc;
                })
                .Publish(context => new IndexTextCommand
                {
                    SessionId = context.Saga.CorrelationId,
                    Source = "user-prompt",
                    Text = context.Message.Prompt
                })
                .Publish(context => ThinkingStatus(context.Saga.CorrelationId))
                .Publish(context => NextTurn(context.Saga))
                .TransitionTo(Thinking));

        During(
            Active,
            When(PromptSubmitted)
                .Then(context =>
                {
                    context.Saga.ConversationHistoryJson = ConversationHistory.AppendUserMessage(
                        context.Saga.ConversationHistoryJson,
                        context.Message.Prompt);
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .Publish(context => ThinkingStatus(context.Saga.CorrelationId))
                .Publish(context => NextTurn(context.Saga))
                .TransitionTo(Thinking));

        During(
            Thinking,
            AwaitingTool,
            When(PromptSubmitted)
                .Publish(context => new AgentStatusChangedEvent
                {
                    SessionId = context.Saga.CorrelationId,
                    Status = AgentStatus.Failed,
                    Detail = "The agent is busy. Wait for the current turn to finish."
                }));

        During(
            Thinking,
            When(TurnCompleted, context => context.Message.FailureReason is not null)
                .Then(context =>
                {
                    context.Saga.ConversationHistoryJson = context.Message.ConversationHistoryJson;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .Publish(context => new AgentStatusChangedEvent
                {
                    SessionId = context.Saga.CorrelationId,
                    Status = AgentStatus.Failed,
                    Detail = context.Message.FailureReason
                })
                .TransitionTo(Active),
            When(TurnCompleted, context => context.Message.FailureReason is null && context.Message.ToolName is not null)
                .Then(context =>
                {
                    context.Saga.ConversationHistoryJson = context.Message.ConversationHistoryJson;
                    context.Saga.LastStateSnapshot = PendingToolCall.Serialize(
                        new PendingToolCall(
                            context.Message.ToolCallId ?? string.Empty,
                            context.Message.ToolName!,
                            context.Message.ArgumentsJson ?? "{}"));
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .Publish(context => new AgentStatusChangedEvent
                {
                    SessionId = context.Saga.CorrelationId,
                    Status = AgentStatus.ExecutingTool,
                    Detail = context.Message.ToolName
                })
                .IfElse(
                    context => context.Message.ToolName == ToolNames.SearchMemory,
                    memory => memory.Publish(context => new SearchMemoryCommand
                    {
                        SessionId = context.Saga.CorrelationId,
                        ToolCallId = context.Message.ToolCallId ?? string.Empty,
                        ArgumentsJson = context.Message.ArgumentsJson ?? "{}"
                    }),
                    other => other.Publish(context => new ExecuteToolCommand
                    {
                        SessionId = context.Saga.CorrelationId,
                        ToolCallId = context.Message.ToolCallId ?? string.Empty,
                        ToolName = context.Message.ToolName!,
                        ArgumentsJson = context.Message.ArgumentsJson ?? "{}"
                    }))
                .TransitionTo(AwaitingTool),
            When(TurnCompleted, context => context.Message.FailureReason is null && context.Message.ToolName is null)
                .Then(context =>
                {
                    context.Saga.ConversationHistoryJson = context.Message.ConversationHistoryJson;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .Publish(context => new AgentTokenGeneratedEvent
                {
                    SessionId = context.Saga.CorrelationId,
                    IsFinal = true
                })
                .TransitionTo(Active));

        During(
            AwaitingTool,
            When(ToolExecuted, context => PendingToolCall.Matches(context.Saga.LastStateSnapshot, context.Message.ToolCallId))
                .Then(context =>
                {
                    context.Saga.ConversationHistoryJson = ConversationHistory.AppendToolResult(
                        context.Saga.ConversationHistoryJson,
                        context.Message.ToolCallId,
                        context.Message.Output);
                    context.Saga.LastStateSnapshot = null;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .Publish(context => ThinkingStatus(context.Saga.CorrelationId))
                .Publish(context => NextTurn(context.Saga))
                .TransitionTo(Thinking));

        DuringAny(Ignore(SessionStarted));
    }

    /// <summary>Gets the state in which the session waits for a user prompt.</summary>
    public State Active { get; private set; } = null!;

    /// <summary>Gets the state in which the model generates a response.</summary>
    public State Thinking { get; private set; } = null!;

    /// <summary>Gets the state in which the agent is frozen until a tool result arrives.</summary>
    public State AwaitingTool { get; private set; } = null!;

    /// <summary>Gets the event raised when a client starts a session.</summary>
    public Event<StartAgentSessionCommand> SessionStarted { get; private set; } = null!;

    /// <summary>Gets the event raised when a client sends a subsequent prompt.</summary>
    public Event<SubmitAgentPromptCommand> PromptSubmitted { get; private set; } = null!;

    /// <summary>Gets the event raised when a reasoning turn finishes.</summary>
    public Event<AgentTurnCompletedEvent> TurnCompleted { get; private set; } = null!;

    /// <summary>Gets the event raised when the Tool Worker reports a tool result.</summary>
    public Event<ToolExecutedEvent> ToolExecuted { get; private set; } = null!;

    private static AgentStatusChangedEvent ThinkingStatus(Guid sessionId)
    {
        return new AgentStatusChangedEvent { SessionId = sessionId, Status = AgentStatus.Thinking };
    }

    private static RunAgentTurnCommand NextTurn(AgentSessionState state)
    {
        return new RunAgentTurnCommand
        {
            SessionId = state.CorrelationId,
            ConversationHistoryJson = state.ConversationHistoryJson
        };
    }
}
