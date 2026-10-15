using System.Threading.Channels;
using Semantic.Engine.Client.Cli.Gateway;
using Semantic.Engine.Client.Cli.Input;
using Semantic.Engine.Client.Cli.Rendering;
using Spectre.Console;

namespace Semantic.Engine.Client.Cli;

/// <summary>
/// The interactive loop of the terminal client.
/// </summary>
public sealed class ChatSession(IAnsiConsole console, GatewayClient gateway, TurnRenderer renderer)
{
    private const string ExitCommand = "/exit";
    private const string NewSessionCommand = "/new";

    /// <summary>
    /// Runs the loop until the user exits.
    /// </summary>
    /// <param name="cancellationToken">A token that stops the session.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        PrintBanner();

        Guid? sessionId = null;
        ChannelReader<AgentStreamEvent>? reader = null;
        using CancellationTokenSource streamLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationTokenSource sessionScope = streamLifetime;

        while (!cancellationToken.IsCancellationRequested)
        {
            console.Markup("\n[green]User[/] [grey](Ctrl+Enter to submit, /new for a new session, /exit to quit)[/]\n");
            string prompt = MultiLinePromptReader.Read();

            if (prompt.Length == 0)
            {
                continue;
            }

            if (prompt.Equals(ExitCommand, StringComparison.OrdinalIgnoreCase) || prompt.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (prompt.Equals(NewSessionCommand, StringComparison.OrdinalIgnoreCase))
            {
                await sessionScope.CancelAsync();
                sessionScope.Dispose();
                sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                sessionId = null;
                reader = null;
                console.MarkupLine("[grey]A new session starts with the next prompt.[/]");
                continue;
            }

            try
            {
                if (sessionId is null)
                {
                    sessionId = await gateway.StartSessionAsync(prompt, cancellationToken);
                    reader = gateway.OpenStream(sessionId.Value, sessionScope.Token);
                }
                else
                {
                    await gateway.SubmitPromptAsync(sessionId.Value, prompt, cancellationToken);
                }

                await renderer.RenderTurnAsync(reader!, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
            {
                console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");

                // A broken stream cannot be resumed, so the next prompt starts a fresh session
                await sessionScope.CancelAsync();
                sessionScope.Dispose();
                sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                sessionId = null;
                reader = null;
            }
        }

        await sessionScope.CancelAsync();
    }

    private void PrintBanner()
    {
        console.Write(new Rule("[bold cyan]Semantic.Engine[/]").RuleStyle("grey"));
        console.MarkupLine("[grey]Distributed multi-agent runtime. Paste code and press Ctrl+Enter.[/]");
    }
}
