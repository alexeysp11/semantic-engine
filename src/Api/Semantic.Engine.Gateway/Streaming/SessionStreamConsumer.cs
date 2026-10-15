using System.Text.Json;
using MassTransit;
using Semantic.Engine.Contracts.Events;

namespace Semantic.Engine.Gateway.Streaming;

/// <summary>
/// Bridges bus events to the in-memory <see cref="SessionStreamHub"/> that feeds open SSE connections.
/// </summary>
/// <remarks>
/// Both event types are handled by one consumer on a single-consumer endpoint so that tokens and status changes
/// reach the client in the order in which the bus delivered them.
/// </remarks>
public sealed class SessionStreamConsumer(SessionStreamHub hub) :
    IConsumer<AgentTokenGeneratedEvent>,
    IConsumer<AgentStatusChangedEvent>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public Task Consume(ConsumeContext<AgentTokenGeneratedEvent> context)
    {
        AgentTokenGeneratedEvent message = context.Message;

        if (message.Token.Length > 0)
        {
            hub.Publish(message.SessionId, new SessionStreamEvent
            {
                Name = SessionStreamEvent.TokenEvent,
                DataJson = JsonSerializer.Serialize(new { text = message.Token }, JsonOptions)
            });
        }

        if (message.IsFinal)
        {
            hub.Publish(message.SessionId, new SessionStreamEvent { Name = SessionStreamEvent.DoneEvent });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task Consume(ConsumeContext<AgentStatusChangedEvent> context)
    {
        AgentStatusChangedEvent message = context.Message;
        hub.Publish(message.SessionId, new SessionStreamEvent
        {
            Name = SessionStreamEvent.StatusEvent,
            DataJson = JsonSerializer.Serialize(new { status = message.Status.ToString(), detail = message.Detail }, JsonOptions)
        });

        return Task.CompletedTask;
    }
}
