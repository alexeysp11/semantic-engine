using System.Globalization;
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Semantic.Engine.Contracts.Commands;
using Semantic.Engine.Contracts.Events;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Workers.Data.Options;
using Semantic.Engine.Workers.Data.Persistence;

namespace Semantic.Engine.Workers.Data.Consumers;

/// <summary>
/// Answers the <c>search_memory</c> tool: embeds the query and returns the most similar stored chunks of the session.
/// </summary>
/// <remarks>
/// The consumer always publishes a <see cref="ToolExecutedEvent"/>, even on failure, because the saga is frozen until
/// the result arrives.
/// </remarks>
public sealed class SearchMemoryConsumer(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    DataDbContext dbContext,
    IOptions<DataWorkerOptions> options,
    ILogger<SearchMemoryConsumer> logger) : IConsumer<SearchMemoryCommand>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<SearchMemoryCommand> context)
    {
        SearchMemoryCommand command = context.Message;
        (bool succeeded, string output) = await SearchAsync(command, context.CancellationToken);

        await context.Publish(
            new ToolExecutedEvent
            {
                SessionId = command.SessionId,
                ToolCallId = command.ToolCallId,
                ToolName = ToolNames.SearchMemory,
                Succeeded = succeeded,
                Output = output
            },
            context.CancellationToken);
    }

    private async Task<(bool Succeeded, string Output)> SearchAsync(SearchMemoryCommand command, CancellationToken cancellationToken)
    {
        DataWorkerOptions settings = options.Value;

        string? query;
        int topK = settings.SearchDefaultTopK;
        try
        {
            using JsonDocument arguments = JsonDocument.Parse(command.ArgumentsJson);
            query = arguments.RootElement.TryGetProperty("query", out JsonElement queryElement) && queryElement.ValueKind == JsonValueKind.String
                ? queryElement.GetString()
                : null;

            if (arguments.RootElement.TryGetProperty("top_k", out JsonElement topKElement)
                && topKElement.ValueKind == JsonValueKind.Number
                && topKElement.TryGetInt32(out int requested))
            {
                topK = Math.Clamp(requested, 1, settings.SearchMaxTopK);
            }
        }
        catch (JsonException)
        {
            return (false, "The tool arguments are not valid JSON.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return (false, "The required argument 'query' is missing or empty.");
        }

        try
        {
            GeneratedEmbeddings<Embedding<float>> embeddings = await embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);
            Vector queryVector = new(embeddings[0].Vector);

            List<MemoryMatch> matches = await dbContext.MemoryChunks
                .Where(chunk => chunk.SessionId == command.SessionId)
                .OrderBy(chunk => chunk.Embedding.CosineDistance(queryVector))
                .Take(topK)
                .Select(chunk => new MemoryMatch(chunk.Source, chunk.Content, chunk.Embedding.CosineDistance(queryVector)))
                .ToListAsync(cancellationToken);

            if (matches.Count == 0)
            {
                return (true, "No memories are stored for this session yet.");
            }

            StringBuilder output = new();
            for (int index = 0; index < matches.Count; index++)
            {
                double similarity = 1.0 - matches[index].Distance;
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"[{index + 1}] source={matches[index].Source} similarity={similarity:F2}"));
                output.AppendLine(matches[index].Content);
                output.AppendLine();
            }

            string text = output.ToString().TrimEnd();
            return (true, text.Length <= settings.SearchMaxOutputCharacters
                ? text
                : string.Concat(text.AsSpan(0, settings.SearchMaxOutputCharacters), "\n...[truncated]"));
        }
        catch (Exception exception) when (exception is HttpRequestException or DbUpdateException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Session {SessionId}: memory search failed", command.SessionId);
            return (false, $"Memory search failed: {exception.Message}");
        }
    }
}

/// <summary>
/// One stored chunk returned by a similarity query.
/// </summary>
/// <param name="Source">The origin label of the chunk.</param>
/// <param name="Content">The chunk text.</param>
/// <param name="Distance">The cosine distance to the query; smaller is more similar.</param>
internal sealed record MemoryMatch(string Source, string Content, double Distance);
