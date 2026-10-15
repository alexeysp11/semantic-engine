using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Threading.Channels;
using Semantic.Engine.Client.Cli.Options;

namespace Semantic.Engine.Client.Cli.Gateway;

/// <summary>
/// HTTP client of the Gateway. It is the only component of the CLI that talks to the backend.
/// </summary>
public sealed class GatewayClient(HttpClient httpClient, GatewayOptions options) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Starts a session.
    /// </summary>
    /// <param name="prompt">The first prompt.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The identifier of the created session.</returns>
    public async Task<Guid> StartSessionAsync(string prompt, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CreateTimeout(cancellationToken);
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "/api/agent/session/start",
            new { prompt },
            JsonOptions,
            timeout.Token);
        response.EnsureSuccessStatusCode();

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(timeout.Token),
            cancellationToken: timeout.Token);
        return body.RootElement.GetProperty("sessionId").GetGuid();
    }

    /// <summary>
    /// Sends a prompt into an existing session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="prompt">The prompt.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    public async Task SubmitPromptAsync(Guid sessionId, string prompt, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CreateTimeout(cancellationToken);
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "/api/agent/prompt",
            new { sessionId, prompt },
            JsonOptions,
            timeout.Token);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Opens the session stream and forwards its events into a channel until the connection ends.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">A token that closes the connection.</param>
    /// <returns>A reader that yields parsed events; it completes with an error when the connection fails.</returns>
    public ChannelReader<AgentStreamEvent> OpenStream(Guid sessionId, CancellationToken cancellationToken)
    {
        Channel<AgentStreamEvent> channel = Channel.CreateUnbounded<AgentStreamEvent>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        _ = PumpAsync(sessionId, channel.Writer, cancellationToken);
        return channel.Reader;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        httpClient.Dispose();
    }

    private async Task PumpAsync(Guid sessionId, ChannelWriter<AgentStreamEvent> writer, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"/api/agent/session/{sessionId}/stream");
            request.Headers.Accept.ParseAdd("text/event-stream");

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await foreach (SseItem<string> item in SseParser.Create(stream).EnumerateAsync(cancellationToken))
            {
                AgentStreamEvent? parsed = StreamEventParser.Parse(item.EventType, item.Data);
                if (parsed is not null)
                {
                    await writer.WriteAsync(parsed, cancellationToken);
                }
            }

            writer.TryComplete(new IOException("The Gateway closed the event stream."));
        }
        catch (OperationCanceledException)
        {
            writer.TryComplete();
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        {
            writer.TryComplete(exception);
        }
    }

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
        return source;
    }
}
