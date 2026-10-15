using System.Text;
using System.Threading.Channels;
using Semantic.Engine.Client.Cli.Gateway;
using Semantic.Engine.Client.Cli.Options;
using Spectre.Console;

namespace Semantic.Engine.Client.Cli.Rendering;

/// <summary>
/// Renders one agent turn: an animated spinner while the agent works and a live panel while text streams in.
/// </summary>
/// <remarks>
/// Spectre.Console does not allow a status spinner and a live display at the same time, so the renderer alternates
/// between the two. An agent turn can contain several waiting and streaming phases, for example when the agent builds
/// the code between two messages.
/// </remarks>
public sealed class TurnRenderer(IAnsiConsole console, RenderingOptions options)
{
    /// <summary>
    /// Renders events until the turn ends.
    /// </summary>
    /// <param name="reader">The session event reader.</param>
    /// <param name="cancellationToken">A token that aborts the rendering.</param>
    /// <returns><see langword="true"/> when the turn completed successfully; <see langword="false"/> when it failed.</returns>
    /// <exception cref="IOException">The event stream ended before the turn completed.</exception>
    public async Task<bool> RenderTurnAsync(ChannelReader<AgentStreamEvent> reader, CancellationToken cancellationToken)
    {
        string caption = StatusText.Initial;

        while (true)
        {
            AgentStreamEvent next = await WaitForContentAsync(reader, caption, cancellationToken);

            if (next.IsFailure)
            {
                console.MarkupLine($"[red]Agent failed:[/] {Markup.Escape(next.Detail ?? "unknown error")}");
                return false;
            }

            if (next.Kind == AgentStreamEventKind.Done)
            {
                return true;
            }

            AgentStreamEvent? interruption = await StreamTextAsync(reader, next.Text, cancellationToken);
            if (interruption is null || interruption.Kind == AgentStreamEventKind.Done)
            {
                return true;
            }

            if (interruption.IsFailure)
            {
                console.MarkupLine($"[red]Agent failed:[/] {Markup.Escape(interruption.Detail ?? "unknown error")}");
                return false;
            }

            caption = StatusText.Format(interruption);
        }
    }

    private async Task<AgentStreamEvent> WaitForContentAsync(
        ChannelReader<AgentStreamEvent> reader,
        string initialCaption,
        CancellationToken cancellationToken)
    {
        return await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(initialCaption, async context =>
            {
                while (true)
                {
                    AgentStreamEvent streamEvent = await ReadAsync(reader, cancellationToken);
                    if (streamEvent.Kind == AgentStreamEventKind.Status && !streamEvent.IsFailure)
                    {
                        context.Status(StatusText.Format(streamEvent));
                        continue;
                    }

                    return streamEvent;
                }
            });
    }

    private async Task<AgentStreamEvent?> StreamTextAsync(
        ChannelReader<AgentStreamEvent> reader,
        string firstText,
        CancellationToken cancellationToken)
    {
        StringBuilder text = new(firstText);
        TimeSpan refreshInterval = TimeSpan.FromMilliseconds(options.LiveRefreshMilliseconds);

        return await console.Live(ResponseRenderer.Render(text.ToString()))
            .StartAsync(async context =>
            {
                DateTime lastRefresh = DateTime.UtcNow;

                while (true)
                {
                    AgentStreamEvent streamEvent = await ReadAsync(reader, cancellationToken);

                    if (streamEvent.Kind == AgentStreamEventKind.Token)
                    {
                        text.Append(streamEvent.Text);
                        if (DateTime.UtcNow - lastRefresh >= refreshInterval)
                        {
                            context.UpdateTarget(ResponseRenderer.Render(text.ToString()));
                            lastRefresh = DateTime.UtcNow;
                        }

                        continue;
                    }

                    // Draw the complete text before the live display is released
                    context.UpdateTarget(ResponseRenderer.Render(text.ToString()));
                    context.Refresh();
                    return streamEvent;
                }
            });
    }

    private static async Task<AgentStreamEvent> ReadAsync(ChannelReader<AgentStreamEvent> reader, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException exception)
        {
            throw new IOException("The connection to the Gateway was lost.", exception.InnerException ?? exception);
        }
    }
}
