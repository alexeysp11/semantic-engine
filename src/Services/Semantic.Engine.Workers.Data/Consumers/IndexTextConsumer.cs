using MassTransit;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Pgvector;
using Semantic.Engine.Contracts.Commands;
using Semantic.Engine.Contracts.Events;
using Semantic.Engine.Workers.Data.Embedding;
using Semantic.Engine.Workers.Data.Options;
using Semantic.Engine.Workers.Data.Persistence;

namespace Semantic.Engine.Workers.Data.Consumers;

/// <summary>
/// Chunks a text, generates an embedding for every chunk and stores the chunks in the pgvector table.
/// </summary>
public sealed class IndexTextConsumer(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    DataDbContext dbContext,
    IOptions<DataWorkerOptions> options,
    ILogger<IndexTextConsumer> logger) : IConsumer<IndexTextCommand>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<IndexTextCommand> context)
    {
        IndexTextCommand command = context.Message;
        DataWorkerOptions settings = options.Value;

        IReadOnlyList<string> chunks = TextChunker.Split(command.Text, settings.ChunkSizeCharacters, settings.ChunkOverlapCharacters);
        if (chunks.Count == 0)
        {
            logger.LogWarning("Session {SessionId}: nothing to index for source {Source}", command.SessionId, command.Source);
            return;
        }

        GeneratedEmbeddings<Embedding<float>> embeddings = await embeddingGenerator.GenerateAsync(chunks, cancellationToken: context.CancellationToken);

        DateTime now = DateTime.UtcNow;
        for (int index = 0; index < chunks.Count; index++)
        {
            dbContext.MemoryChunks.Add(new MemoryChunk
            {
                Id = Guid.NewGuid(),
                SessionId = command.SessionId,
                Source = command.Source,
                ChunkIndex = index,
                Content = chunks[index],
                Embedding = new Vector(embeddings[index].Vector),
                CreatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Session {SessionId}: indexed {Count} chunk(s) from {Source}", command.SessionId, chunks.Count, command.Source);

        await context.Publish(
            new TextIndexedEvent { SessionId = command.SessionId, Source = command.Source, ChunkCount = chunks.Count },
            context.CancellationToken);
    }
}
