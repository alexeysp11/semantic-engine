using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Semantic.Engine.Orchestrator.Context;
using Semantic.Engine.Orchestrator.Options;
using Semantic.Engine.Orchestrator.Telemetry;
using Semantic.Engine.Orchestrator.Tools;

namespace Semantic.Engine.Orchestrator.Agent;

/// <summary>
/// Executes one iteration of the agent loop: prunes the context, streams the model response and extracts a tool call.
/// </summary>
/// <remarks>
/// The service never executes tools. A tool call is validated by the send-safety guard and returned to the caller,
/// which publishes it to the bus and freezes the session until the tool result arrives.
/// </remarks>
public sealed class AgentReasoningService(
    IChatClient chatClient,
    IContextManager contextManager,
    IThoughtLogWriter thoughtLog,
    IPromptProvider prompts,
    IOptions<AiSettingsOptions> options,
    ILogger<AgentReasoningService> logger)
{
    /// <summary>
    /// Runs a reasoning turn.
    /// </summary>
    /// <param name="sessionId">The identifier of the session.</param>
    /// <param name="historyJson">The serialized conversation history without the system prompt.</param>
    /// <param name="onToken">A callback that receives every streamed text fragment of the model.</param>
    /// <param name="cancellationToken">A token that cancels the turn.</param>
    /// <returns>The updated history and the requested tool call, if any.</returns>
    public async Task<AgentTurnResult> RunTurnAsync(
        Guid sessionId,
        string historyJson,
        Func<string, CancellationToken, Task> onToken,
        CancellationToken cancellationToken)
    {
        AiSettingsOptions settings = options.Value;

        IReadOnlyList<ChatMessage> prunedHistory = await contextManager.PruneAsync(
            sessionId,
            ConversationHistory.Deserialize(historyJson),
            cancellationToken);
        List<ChatMessage> history = [.. prunedHistory];

        ChatOptions chatOptions = new()
        {
            ModelId = settings.ModelId,
            Temperature = settings.Temperature,
            Tools = ToolCatalog.CreateDeclarations()
        };

        for (int rejectedCalls = 0; ; rejectedCalls++)
        {
            List<ChatMessage> request = [new ChatMessage(ChatRole.System, prompts.AgentPrompt), .. history];

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<ChatResponseUpdate> updates = [];
            ToolCallStreamFilter streamFilter = new();
            await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(request, chatOptions, cancellationToken))
            {
                updates.Add(update);
                if (!string.IsNullOrEmpty(update.Text))
                {
                    string visible = streamFilter.Push(update.Text);
                    if (visible.Length > 0)
                    {
                        await onToken(visible, cancellationToken);
                    }
                }
            }

            ChatResponse response = updates.ToChatResponse();
            (ChatMessage assistantMessage, RequestedToolCall? toolCall) = ExtractAssistantMessage(response);
            history.Add(assistantMessage);

            // Text that looked like a tool call but was not one is shown after all
            string released = streamFilter.Complete(toolCall is not null);
            if (released.Length > 0)
            {
                await onToken(released, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(assistantMessage.Text))
            {
                await thoughtLog.WriteAsync(
                    new ThoughtLogEntry
                    {
                        SessionId = sessionId,
                        StepType = ThoughtStepTypes.AssistantMessage,
                        Content = assistantMessage.Text,
                        DurationMs = (uint)stopwatch.ElapsedMilliseconds
                    },
                    cancellationToken);
            }

            if (toolCall is null)
            {
                return new AgentTurnResult { ConversationHistoryJson = ConversationHistory.Serialize(history) };
            }

            ToolCallValidationResult validation = ToolCallGuard.Validate(toolCall.Name, toolCall.ArgumentsJson);
            if (validation.IsAllowed)
            {
                await thoughtLog.WriteAsync(
                    new ThoughtLogEntry
                    {
                        SessionId = sessionId,
                        StepType = ThoughtStepTypes.ToolCall,
                        ToolName = toolCall.Name,
                        Content = toolCall.ArgumentsJson
                    },
                    cancellationToken);

                return new AgentTurnResult
                {
                    ConversationHistoryJson = ConversationHistory.Serialize(history),
                    ToolCall = toolCall
                };
            }

            logger.LogWarning(
                "Session {SessionId}: tool call {Tool} rejected by send-safety: {Reason}",
                sessionId,
                toolCall.Name,
                validation.RejectionReason);

            await thoughtLog.WriteAsync(
                new ThoughtLogEntry
                {
                    SessionId = sessionId,
                    StepType = ThoughtStepTypes.ToolRejected,
                    ToolName = toolCall.Name,
                    Content = validation.RejectionReason ?? string.Empty
                },
                cancellationToken);

            // Feed the rejection back to the model so that it can correct the call within the same turn
            history.Add(new ChatMessage(
                ChatRole.Tool,
                [new FunctionResultContent(toolCall.CallId, $"Rejected by send-safety validation: {validation.RejectionReason}")]));

            if (rejectedCalls + 1 >= settings.MaxRejectedToolCalls)
            {
                return new AgentTurnResult
                {
                    ConversationHistoryJson = ConversationHistory.Serialize(history),
                    FailureReason = "The model repeatedly produced invalid tool calls."
                };
            }
        }
    }

    private static (ChatMessage Message, RequestedToolCall? ToolCall) ExtractAssistantMessage(ChatResponse response)
    {
        List<AIContent> contents = response.Messages.SelectMany(message => message.Contents).ToList();
        FunctionCallContent? nativeCall = contents.OfType<FunctionCallContent>().FirstOrDefault();

        if (nativeCall is not null)
        {
            // Only the first call is honored so that every call in the history has a matching result
            string callId = string.IsNullOrEmpty(nativeCall.CallId) ? NewCallId() : nativeCall.CallId;
            List<AIContent> kept = contents.Where(content => content is not FunctionCallContent).ToList();
            kept.Add(new FunctionCallContent(callId, nativeCall.Name, nativeCall.Arguments));

            string argumentsJson = JsonSerializer.Serialize(
                nativeCall.Arguments ?? new Dictionary<string, object?>(),
                AIJsonUtilities.DefaultOptions);
            return (
                new ChatMessage(ChatRole.Assistant, kept),
                new RequestedToolCall(callId, nativeCall.Name, argumentsJson));
        }

        if (TextToolCallParser.TryParse(response.Text, name => ToolCatalog.Find(name) is not null, out ParsedToolCall parsed))
        {
            string callId = NewCallId();
            Dictionary<string, object?>? arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                parsed.ArgumentsJson,
                AIJsonUtilities.DefaultOptions);

            List<AIContent> recovered = [];
            if (parsed.RemainingText.Length > 0)
            {
                recovered.Add(new TextContent(parsed.RemainingText));
            }

            recovered.Add(new FunctionCallContent(callId, parsed.Name, arguments));
            return (
                new ChatMessage(ChatRole.Assistant, recovered),
                new RequestedToolCall(callId, parsed.Name, parsed.ArgumentsJson));
        }

        return (new ChatMessage(ChatRole.Assistant, contents), null);
    }

    private static string NewCallId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
