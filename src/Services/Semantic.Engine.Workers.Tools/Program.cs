using MassTransit;
using Microsoft.Extensions.Options;
using Semantic.Engine.Workers.Tools.Consumers;
using Semantic.Engine.Workers.Tools.Dotnet;
using Semantic.Engine.Workers.Tools.Options;
using Semantic.Engine.Workers.Tools.Tools;
using Semantic.Engine.Workers.Tools.Workspace;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<ToolsWorkerOptions>().Bind(builder.Configuration.GetSection(ToolsWorkerOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<RabbitMqOptions>().Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddHttpClient(HttpRequestTool.HttpClientName, (provider, client) =>
    {
        client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<ToolsWorkerOptions>>().Value.HttpTimeoutSeconds);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

builder.Services.AddSingleton<WorkspacePathResolver>();
builder.Services.AddSingleton<DotnetCommandRunner>();
builder.Services.AddSingleton<DotnetWorkspaceExecutor>();
builder.Services.AddSingleton<ITool, ListFilesTool>();
builder.Services.AddSingleton<ITool, ReadFileTool>();
builder.Services.AddSingleton<ITool, WriteFileTool>();
builder.Services.AddSingleton<ITool, PatchFileTool>();
builder.Services.AddSingleton<ITool, InsertAtLineTool>();
builder.Services.AddSingleton<ITool, HttpRequestTool>();
builder.Services.AddSingleton<ITool, DotnetBuildTool>();
builder.Services.AddSingleton<ITool, DotnetTestTool>();

builder.Services.AddMassTransit(configurator =>
{
    configurator.SetKebabCaseEndpointNameFormatter();
    configurator.AddConsumer<ExecuteToolConsumer>();

    configurator.UsingRabbitMq((context, bus) =>
    {
        RabbitMqOptions rabbit = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        bus.Host(rabbit.Host, rabbit.VirtualHost, host =>
        {
            host.Username(rabbit.Username);
            host.Password(rabbit.Password);
        });

        bus.ConfigureEndpoints(context);
    });
});

IHost host = builder.Build();
await host.RunAsync();
