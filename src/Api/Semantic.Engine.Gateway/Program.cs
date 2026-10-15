using MassTransit;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Semantic.Engine.Gateway.Endpoints;
using Semantic.Engine.Gateway.Options;
using Semantic.Engine.Gateway.Streaming;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<RabbitMqOptions>().Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SecurityOptions>().Bind(builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.AddOptions<StreamingOptions>().Bind(builder.Configuration.GetSection(StreamingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SessionStreamHub>();
builder.Services.AddHostedService<SessionStreamSweeper>();

builder.Services.AddMassTransit(configurator =>
{
    configurator.AddConsumer<SessionStreamConsumer>();

    configurator.UsingRabbitMq((context, bus) =>
    {
        RabbitMqOptions rabbit = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        bus.Host(rabbit.Host, rabbit.VirtualHost, host =>
        {
            host.Username(rabbit.Username);
            host.Password(rabbit.Password);
        });

        // Every Gateway instance owns a private queue so that it receives all events of the sessions it serves
        bus.ReceiveEndpoint($"gateway-stream-{Guid.NewGuid():N}", endpoint =>
        {
            endpoint.Durable = false;
            endpoint.AutoDelete = true;
            endpoint.ConcurrentMessageLimit = 1;
            endpoint.ConfigureConsumer<SessionStreamConsumer>(context);
        });
    });
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapAgentEndpoints();

await app.RunAsync();
