using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Semantic.Engine.Contracts.Commands;
using Semantic.Engine.Gateway.Options;
using Semantic.Engine.Gateway.Streaming;

namespace Semantic.Engine.Gateway.Endpoints;

/// <summary>
/// Maps the agent session endpoints of the Gateway.
/// </summary>
public static class AgentEndpoints
{
    /// <summary>
    /// Registers the agent endpoints under <c>/api/agent</c>.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/agent")
            .WithTags("Agent")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapPost("/session/start", StartSessionAsync);
        group.MapPost("/prompt", SubmitPromptAsync);
        group.MapGet("/session/{sessionId:guid}/stream", StreamSessionAsync);

        return app;
    }

    /// <summary>Starts a session by publishing a <see cref="StartAgentSessionCommand"/>.</summary>
    [EndpointSummary("Starts an agent session")]
    [EndpointDescription("Creates a session, publishes the first prompt to the bus and returns the session identifier. Open the stream endpoint with the identifier to receive the agent output.")]
    [ProducesResponseType<StartSessionResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    internal static async Task<Results<Accepted<StartSessionResponse>, ValidationProblem>> StartSessionAsync(
        StartSessionRequest request,
        SessionStreamHub hub,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { [nameof(request.Prompt)] = ["The prompt must not be empty."] });
        }

        Guid sessionId = Guid.NewGuid();

        // The stream exists before the command is published so that early tokens are buffered
        hub.Open(sessionId);
        await publishEndpoint.Publish(
            new StartAgentSessionCommand { SessionId = sessionId, Prompt = request.Prompt },
            cancellationToken);

        return TypedResults.Accepted($"/api/agent/session/{sessionId}/stream", new StartSessionResponse { SessionId = sessionId });
    }

    /// <summary>Delivers a prompt into an existing session by publishing a <see cref="SubmitAgentPromptCommand"/>.</summary>
    [EndpointSummary("Sends a prompt into a session")]
    [EndpointDescription("Delivers a subsequent prompt into the saga of an existing session. The agent output arrives on the session stream.")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    internal static async Task<Results<Accepted, ValidationProblem>> SubmitPromptAsync(
        SubmitPromptRequest request,
        SessionStreamHub hub,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = [];
        if (request.SessionId == Guid.Empty)
        {
            errors[nameof(request.SessionId)] = ["The session identifier must not be empty."];
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            errors[nameof(request.Prompt)] = ["The prompt must not be empty."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        hub.Open(request.SessionId);
        await publishEndpoint.Publish(
            new SubmitAgentPromptCommand { SessionId = request.SessionId, Prompt = request.Prompt },
            cancellationToken);

        return TypedResults.Accepted(uri: (string?)null);
    }

    /// <summary>Streams agent events of a session as Server-Sent Events.</summary>
    [EndpointSummary("Streams session events")]
    [EndpointDescription("Opens a Server-Sent Events connection. Events: 'token' (model text fragment), 'status' (agent status change) and 'done' (end of the agent turn). One client can be connected to a session at a time; an unknown session or a session that already has a reader returns 404.")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK, "text/event-stream")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    internal static async Task StreamSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        SessionStreamHub hub,
        IOptions<StreamingOptions> options)
    {
        HttpResponse response = httpContext.Response;
        CancellationToken cancellationToken = httpContext.RequestAborted;

        if (!hub.TryAttach(sessionId, out SessionStreamHub.StreamLease? lease))
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using (lease)
        {
            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            response.Headers["X-Accel-Buffering"] = "no";
            await response.Body.FlushAsync(cancellationToken);

            TimeSpan heartbeat = TimeSpan.FromSeconds(options.Value.HeartbeatSeconds);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    using CancellationTokenSource idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    idle.CancelAfter(heartbeat);

                    SessionStreamEvent streamEvent;
                    try
                    {
                        streamEvent = await lease!.Reader.ReadAsync(idle.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Keep-alive comment so that proxies and clients do not close an idle connection
                        await response.WriteAsync(": keep-alive\n\n", cancellationToken);
                        await response.Body.FlushAsync(cancellationToken);
                        continue;
                    }

                    await response.WriteAsync(FormatEvent(streamEvent), cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // The client disconnected
            }
        }
    }

    internal static string FormatEvent(SessionStreamEvent streamEvent)
    {
        return new StringBuilder()
            .Append("event: ").Append(streamEvent.Name).Append('\n')
            .Append("data: ").Append(streamEvent.DataJson).Append("\n\n")
            .ToString();
    }
}
