using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Semantic.Engine.Contracts.Commands;
using Semantic.Engine.Contracts.Events;
using Semantic.Engine.Orchestrator.Agent;
using Semantic.Engine.Orchestrator.Options;
using Semantic.Engine.Orchestrator.Telemetry;

namespace Semantic.Engine.Orchestrator.Context;

/// <summary>
/// Prunes the conversation history by replacing its old part with a model-written checkpoint message.
/// </summary>
/// <remarks>
/// The checkpoint is also published as an <see cref="IndexTextCommand"/> so that the summarized knowledge stays
/// searchable in the vector store after the original messages are dropped from the saga state.
/// </remarks>
public sealed class ContextManager(
    IChatClient chatClient,
    IPublishEndpoint publishEndpoint,
    IThoughtLogWriter thoughtLog,
    IPromptProvider prompts,
    IOptions<ContextManagementOptions> options,
    IOptions<AiSettingsOptions> aiOptions,
    ILogger<ContextManager> logger) : IContextManager
{
    /// <summary>The prefix that marks a checkpoint message inside the history.</summary>
    public const string CheckpointPrefix = "[Conversation checkpoint]";

    /// <inheritdoc />
    public int EstimateTokens(IReadOnlyList<ChatMessage> messages)
    {
        int characters = 0;
        foreach (ChatMessage message in messages)
        {
            characters += RenderMessage(message, int.MaxValue).Length;
        }

        return characters / Math.Max(1, options.Value.CharactersPerToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> PruneAsync(
        Guid sessionId,
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        ContextManagementOptions settings = options.Value;
        bool overMessageLimit = history.Count > settings.MaxMessages;
        bool overTokenLimit = EstimateTokens(history) > settings.MaxEstimatedTokens;
        if (!overMessageLimit && !overTokenLimit)
        {
            return history;
        }

        int splitIndex = FindSplitIndex(history, settings.KeepRecentMessages);
        if (splitIndex <= 0)
        {
            return history;
        }

        await publishEndpoint.Publish(
            new AgentStatusChangedEvent { SessionId = sessionId, Status = AgentStatus.Summarizing },
            cancellationToken);

        List<ChatMessage> oldPart = history.Take(splitIndex).ToList();
        string summary = await SummarizeAsync(oldPart, cancellationToken);

        List<ChatMessage> pruned =
        [
            new ChatMessage(ChatRole.User, $"{CheckpointPrefix}\n{summary}"),
            .. history.Skip(splitIndex)
        ];

        logger.LogInformation(
            "Session {SessionId}: history pruned from {Before} to {After} messages",
            sessionId,
            history.Count,
            pruned.Count);

        await thoughtLog.WriteAsync(
            new ThoughtLogEntry
            {
                SessionId = sessionId,
                StepType = ThoughtStepTypes.SummaryCheckpoint,
                Content = summary
            },
            cancellationToken);

        await publishEndpoint.Publish(
            new IndexTextCommand { SessionId = sessionId, Source = "summary-checkpoint", Text = summary },
            cancellationToken);

        return pruned;
    }

    /// <summary>
    /// Finds the index of the first message that is kept verbatim.
    /// </summary>
    /// <remarks>
    /// A tool result must never be separated from the assistant message that requested it, so the split point
    /// moves backwards until the first kept message is not a tool result.
    /// </remarks>
    internal static int FindSplitIndex(IReadOnlyList<ChatMessage> history, int keepRecent)
    {
        int splitIndex = history.Count - Math.Max(1, keepRecent);
        while (splitIndex > 0 && history[splitIndex].Role == ChatRole.Tool)
        {
            splitIndex--;
        }

        return splitIndex;
    }

    private async Task<string> SummarizeAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        StringBuilder transcript = new();
        foreach (ChatMessage message in messages)
        {
            transcript.AppendLine(RenderMessage(message, options.Value.MaxToolOutputCharactersInTranscript));
        }

        List<ChatMessage> request =
        [
            new ChatMessage(ChatRole.System, prompts.SummarizerPrompt),
            new ChatMessage(ChatRole.User, transcript.ToString())
        ];

        ChatResponse response = await chatClient.GetResponseAsync(
            request,
            new ChatOptions { Temperature = aiOptions.Value.SummarizerTemperature },
            cancellationToken);

        return response.Text.Trim();
    }

    private static string RenderMessage(ChatMessage message, int maxToolOutputCharacters)
    {
        StringBuilder builder = new();
        builder.Append(message.Role.Value).Append(": ");

        foreach (AIContent content in message.Contents)
        {
            switch (content)
            {
                case TextContent text:
                    builder.Append(text.Text);
                    break;
                case FunctionCallContent call:
                    builder.Append("[tool call ").Append(call.Name).Append(' ');
                    builder.Append(JsonSerializer.Serialize(call.Arguments)).Append(']');
                    break;
                case FunctionResultContent result:
                    string output = result.Result?.ToString() ?? string.Empty;
                    if (output.Length > maxToolOutputCharacters)
                    {
                        output = string.Concat(output.AsSpan(0, maxToolOutputCharacters), "...[truncated]");
                    }

                    builder.Append("[tool result ").Append(output).Append(']');
                    break;
            }
        }

        return builder.ToString();
    }
}
