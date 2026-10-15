using System.Net.Sockets;
using System.Text.Json;
using MassTransit;
using Semantic.Engine.Contracts.Commands;
using Semantic.Engine.Contracts.Events;
using Semantic.Engine.Workers.Tools.Tools;
using Semantic.Engine.Workers.Tools.Workspace;

namespace Semantic.Engine.Workers.Tools.Consumers;

/// <summary>
/// Dispatches an <see cref="ExecuteToolCommand"/> to the matching <see cref="ITool"/> and publishes the observation.
/// </summary>
/// <remarks>
/// Every failure is converted to a <see cref="ToolExecutedEvent"/> with <c>Succeeded = false</c>. The saga is frozen
/// until a result arrives, so the consumer must always answer.
/// </remarks>
public sealed class ExecuteToolConsumer(
    IEnumerable<ITool> tools,
    ILogger<ExecuteToolConsumer> logger) : IConsumer<ExecuteToolCommand>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ExecuteToolCommand> context)
    {
        ExecuteToolCommand command = context.Message;
        ToolResult result = await ExecuteAsync(command, context.CancellationToken);

        logger.LogInformation(
            "Session {SessionId}: tool {Tool} finished, succeeded={Succeeded}",
            command.SessionId,
            command.ToolName,
            result.Succeeded);

        await context.Publish(
            new ToolExecutedEvent
            {
                SessionId = command.SessionId,
                ToolCallId = command.ToolCallId,
                ToolName = command.ToolName,
                Succeeded = result.Succeeded,
                Output = result.Output
            },
            context.CancellationToken);
    }

    private async Task<ToolResult> ExecuteAsync(ExecuteToolCommand command, CancellationToken cancellationToken)
    {
        ITool? tool = tools.FirstOrDefault(candidate => candidate.Name == command.ToolName);
        if (tool is null)
        {
            return new ToolResult(false, $"Unknown tool '{command.ToolName}'.");
        }

        try
        {
            using JsonDocument arguments = JsonDocument.Parse(command.ArgumentsJson);
            return await tool.ExecuteAsync(command.SessionId, arguments.RootElement, cancellationToken);
        }
        catch (ToolExecutionException exception)
        {
            return new ToolResult(false, exception.Message);
        }
        catch (JsonException)
        {
            return new ToolResult(false, "The tool arguments are not valid JSON.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException or SocketException)
        {
            logger.LogWarning(exception, "Tool {Tool} failed", command.ToolName);
            return new ToolResult(false, $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
