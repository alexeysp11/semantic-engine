using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Semantic.Engine.Contracts.Events;
using Semantic.Engine.Contracts.Sagas;
using Semantic.Engine.Orchestrator.Options;
using Semantic.Engine.Orchestrator.Persistence;

namespace Semantic.Engine.Orchestrator.StateMachine;

/// <summary>
/// Reports a timeout to sessions that wait for a tool result for too long.
/// </summary>
/// <remarks>
/// A session in the <c>AwaitingTool</c> state is frozen until a <see cref="ToolExecutedEvent"/> arrives. If the worker
/// that owns the tool is down or lost the command, the watchdog publishes a failed result on its behalf so the model
/// can react. The state lives in PostgreSQL, so the watchdog also recovers sessions that were frozen before a restart.
/// </remarks>
public sealed class ToolWatchdogService(
    IServiceScopeFactory scopeFactory,
    IOptions<ToolWatchdogOptions> options,
    ILogger<ToolWatchdogService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(options.Value.ScanIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException or Npgsql.NpgsqlException)
            {
                logger.LogWarning(exception, "The tool watchdog scan failed and will be retried");
            }
        }
    }

    internal async Task ScanAsync(CancellationToken cancellationToken)
    {
        ToolWatchdogOptions settings = options.Value;
        DateTime cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(settings.ToolTimeoutSeconds);

        using IServiceScope scope = scopeFactory.CreateScope();
        OrchestratorDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
        IPublishEndpoint publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        List<AgentSessionState> stalled = await dbContext.Set<AgentSessionState>()
            .AsNoTracking()
            .Where(state => state.CurrentState == nameof(AgentSessionStateMachine.AwaitingTool) && state.UpdatedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        foreach (AgentSessionState state in stalled)
        {
            PendingToolCall? pending = PendingToolCall.Deserialize(state.LastStateSnapshot);
            if (pending is null)
            {
                continue;
            }

            logger.LogWarning(
                "Session {SessionId}: tool {Tool} did not answer within {Timeout} seconds",
                state.CorrelationId,
                pending.ToolName,
                settings.ToolTimeoutSeconds);

            await publishEndpoint.Publish(
                new ToolExecutedEvent
                {
                    SessionId = state.CorrelationId,
                    ToolCallId = pending.ToolCallId,
                    ToolName = pending.ToolName,
                    Succeeded = false,
                    Output = $"The tool '{pending.ToolName}' did not respond within {settings.ToolTimeoutSeconds} seconds. Try a different approach or repeat the call."
                },
                cancellationToken);
        }
    }
}
