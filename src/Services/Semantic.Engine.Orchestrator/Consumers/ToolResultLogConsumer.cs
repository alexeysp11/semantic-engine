using MassTransit;
using Semantic.Engine.Contracts.Events;
using Semantic.Engine.Orchestrator.Telemetry;

namespace Semantic.Engine.Orchestrator.Consumers;

/// <summary>
/// Records every tool observation in the ClickHouse thought log.
/// </summary>
public sealed class ToolResultLogConsumer(IThoughtLogWriter thoughtLog) : IConsumer<ToolExecutedEvent>
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<ToolExecutedEvent> context)
    {
        ToolExecutedEvent message = context.Message;
        return thoughtLog.WriteAsync(
            new ThoughtLogEntry
            {
                SessionId = message.SessionId,
                StepType = ThoughtStepTypes.ToolResult,
                ToolName = message.ToolName,
                Content = message.Output
            },
            context.CancellationToken);
    }
}
