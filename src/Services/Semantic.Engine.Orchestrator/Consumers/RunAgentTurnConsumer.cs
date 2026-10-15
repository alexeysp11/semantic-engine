using MassTransit;
using Semantic.Engine.Contracts.Commands;
using Semantic.Engine.Contracts.Events;
using Semantic.Engine.Orchestrator.Agent;
using Semantic.Engine.Orchestrator.Telemetry;

namespace Semantic.Engine.Orchestrator.Consumers;

/// <summary>
/// Runs one reasoning turn and streams the model output to the bus as <see cref="AgentTokenGeneratedEvent"/> messages.
/// </summary>
public sealed class RunAgentTurnConsumer(
    AgentReasoningService reasoningService,
    IThoughtLogWriter thoughtLog,
    ILogger<RunAgentTurnConsumer> logger) : IConsumer<RunAgentTurnCommand>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<RunAgentTurnCommand> context)
    {
        RunAgentTurnCommand command = context.Message;

        try
        {
            AgentTurnResult result = await reasoningService.RunTurnAsync(
                command.SessionId,
                command.ConversationHistoryJson,
                (token, cancellationToken) => context.Publish(
                    new AgentTokenGeneratedEvent { SessionId = command.SessionId, Token = token },
                    cancellationToken),
                context.CancellationToken);

            await context.Publish(
                new AgentTurnCompletedEvent
                {
                    SessionId = command.SessionId,
                    ConversationHistoryJson = result.ConversationHistoryJson,
                    ToolCallId = result.ToolCall?.CallId,
                    ToolName = result.ToolCall?.Name,
                    ArgumentsJson = result.ToolCall?.ArgumentsJson,
                    FailureReason = result.FailureReason
                },
                context.CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A failed turn must not poison the queue; the saga reports the failure and returns to the Active state
            logger.LogError(exception, "Reasoning turn of session {SessionId} failed", command.SessionId);

            await thoughtLog.WriteAsync(
                new ThoughtLogEntry
                {
                    SessionId = command.SessionId,
                    StepType = ThoughtStepTypes.TurnFailed,
                    Content = exception.Message
                },
                context.CancellationToken);

            await context.Publish(
                new AgentTurnCompletedEvent
                {
                    SessionId = command.SessionId,
                    ConversationHistoryJson = command.ConversationHistoryJson,
                    FailureReason = $"Reasoning turn failed: {exception.Message}"
                },
                context.CancellationToken);
        }
    }
}
