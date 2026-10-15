using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Semantic.Engine.Workers.Data.Consumers;
using Semantic.Engine.Workers.Data.Options;
using Semantic.Engine.Workers.Data.Persistence;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<DataWorkerOptions>().Bind(builder.Configuration.GetSection(DataWorkerOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<RabbitMqOptions>().Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(provider =>
{
    DataWorkerOptions settings = provider.GetRequiredService<IOptions<DataWorkerOptions>>().Value;
    return new OllamaApiClient(new Uri(settings.OllamaEndpoint), settings.EmbeddingModelId);
});

string connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("The connection string 'Postgres' is not configured.");
builder.Services.AddDbContext<DataDbContext>(options => options.UseNpgsql(
    connectionString,
    npgsql =>
    {
        npgsql.UseVector();
        npgsql.MigrationsHistoryTable(DataDbContext.MigrationsHistoryTable);
    }));

builder.Services.AddMassTransit(configurator =>
{
    configurator.SetKebabCaseEndpointNameFormatter();
    configurator.AddConsumer<IndexTextConsumer>();
    configurator.AddConsumer<SearchMemoryConsumer>();

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

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using IServiceScope scope = host.Services.CreateScope();
    DataDbContext dbContext = scope.ServiceProvider.GetRequiredService<DataDbContext>();
    await dbContext.Database.MigrateAsync();
}

await host.RunAsync();
