using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Semantic.Engine.Contracts.Sagas;
using Semantic.Engine.Orchestrator.Agent;
using Semantic.Engine.Orchestrator.Consumers;
using Semantic.Engine.Orchestrator.Context;
using Semantic.Engine.Orchestrator.Options;
using Semantic.Engine.Orchestrator.Persistence;
using Semantic.Engine.Orchestrator.StateMachine;
using Semantic.Engine.Orchestrator.Telemetry;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<AiSettingsOptions>().Bind(builder.Configuration.GetSection(AiSettingsOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ContextManagementOptions>().Bind(builder.Configuration.GetSection(ContextManagementOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ClickHouseOptions>().Bind(builder.Configuration.GetSection(ClickHouseOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ToolWatchdogOptions>().Bind(builder.Configuration.GetSection(ToolWatchdogOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<RabbitMqOptions>().Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddSingleton<IChatClient>(provider =>
{
    AiSettingsOptions settings = provider.GetRequiredService<IOptions<AiSettingsOptions>>().Value;
    HttpClient httpClient = new()
    {
        BaseAddress = new Uri(settings.Endpoint),
        Timeout = TimeSpan.FromMinutes(settings.RequestTimeoutMinutes)
    };
    return new OllamaApiClient(httpClient, settings.ModelId);
});

builder.Services.AddHttpClient<IThoughtLogWriter, ClickHouseThoughtLogWriter>((provider, client) =>
{
    ClickHouseOptions settings = provider.GetRequiredService<IOptions<ClickHouseOptions>>().Value;
    client.BaseAddress = new Uri(settings.Endpoint);
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
});

builder.Services.AddSingleton<IPromptProvider, FilePromptProvider>();
builder.Services.AddScoped<IContextManager, ContextManager>();
builder.Services.AddScoped<AgentReasoningService>();
builder.Services.AddHostedService<ToolWatchdogService>();

string connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("The connection string 'Postgres' is not configured.");
builder.Services.AddDbContext<OrchestratorDbContext>(options => options.UseNpgsql(
    connectionString,
    npgsql => npgsql.MigrationsHistoryTable(OrchestratorDbContext.MigrationsHistoryTable)));

builder.Services.AddMassTransit(configurator =>
{
    configurator.SetKebabCaseEndpointNameFormatter();

    // The local model is CPU bound, so only one reasoning turn runs at a time
    configurator.AddConsumer<RunAgentTurnConsumer>().Endpoint(endpoint => endpoint.ConcurrentMessageLimit = 1);
    configurator.AddConsumer<ToolResultLogConsumer>();

    configurator.AddSagaStateMachine<AgentSessionStateMachine, AgentSessionState>()
        .EntityFrameworkRepository(repository =>
        {
            repository.ConcurrencyMode = ConcurrencyMode.Pessimistic;
            repository.LockStatementProvider = new PostgresLockStatementProvider();
            repository.ExistingDbContext<OrchestratorDbContext>();
        });

    configurator.UsingRabbitMq((context, bus) =>
    {
        RabbitMqOptions rabbit = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        bus.Host(rabbit.Host, rabbit.VirtualHost, host =>
        {
            host.Username(rabbit.Username);
            host.Password(rabbit.Password);
        });

        // Messages published while a saga handles an event leave the process only after the saga state is saved
        bus.UseInMemoryOutbox(context);
        bus.ConfigureEndpoints(context);
    });
});

IHost host = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using IServiceScope scope = host.Services.CreateScope();
    OrchestratorDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
    await dbContext.Database.MigrateAsync();
}

await host.RunAsync();
