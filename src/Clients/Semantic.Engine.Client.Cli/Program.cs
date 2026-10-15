using Microsoft.Extensions.Configuration;
using Semantic.Engine.Client.Cli;
using Semantic.Engine.Client.Cli.Gateway;
using Semantic.Engine.Client.Cli.Options;
using Semantic.Engine.Client.Cli.Rendering;
using Spectre.Console;

IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables(prefix: "SEMANTIC_ENGINE_")
    .AddCommandLine(args)
    .Build();

GatewayOptions gatewayOptions;
RenderingOptions renderingOptions;
try
{
    gatewayOptions = OptionsLoader.Load<GatewayOptions>(configuration, GatewayOptions.SectionName);
    renderingOptions = OptionsLoader.Load<RenderingOptions>(configuration, RenderingOptions.SectionName);
}
catch (InvalidOperationException exception)
{
    AnsiConsole.MarkupLine($"[red]Configuration error:[/] {Markup.Escape(exception.Message)}");
    return 1;
}

using CancellationTokenSource shutdown = new();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

// The event stream is long-lived, so the HTTP client has no global timeout; request timeouts are applied per call
using HttpClient httpClient = new()
{
    BaseAddress = new Uri(gatewayOptions.BaseUrl),
    Timeout = Timeout.InfiniteTimeSpan
};
if (!string.IsNullOrEmpty(gatewayOptions.ApiKey))
{
    httpClient.DefaultRequestHeaders.Add("X-Api-Key", gatewayOptions.ApiKey);
}

using GatewayClient gateway = new(httpClient, gatewayOptions);
TurnRenderer renderer = new(AnsiConsole.Console, renderingOptions);
ChatSession session = new(AnsiConsole.Console, gateway, renderer);

await session.RunAsync(shutdown.Token);
return 0;
