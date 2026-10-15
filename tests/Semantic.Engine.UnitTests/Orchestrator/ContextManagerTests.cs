using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Semantic.Engine.Contracts.Commands;
using Semantic.Engine.Orchestrator.Agent;
using Semantic.Engine.Orchestrator.Context;
using Semantic.Engine.Orchestrator.Options;
using Semantic.Engine.Orchestrator.Telemetry;
using Xunit;

namespace Semantic.Engine.UnitTests.Orchestrator;

public sealed class ContextManagerTests
{
    private readonly Mock<IChatClient> _chatClient = new();
    private readonly Mock<IPublishEndpoint> _publishEndpoint = new();
    private readonly Mock<IThoughtLogWriter> _thoughtLog = new();
    private readonly Mock<IPromptProvider> _prompts = new();

    private ContextManager CreateManager(int maxMessages, int keepRecent)
    {
        _prompts.SetupGet(prompts => prompts.SummarizerPrompt).Returns("Summarize.");
        _chatClient
            .Setup(client => client.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "checkpoint text")));

        return new ContextManager(
            _chatClient.Object,
            _publishEndpoint.Object,
            _thoughtLog.Object,
            _prompts.Object,
            Microsoft.Extensions.Options.Options.Create(new ContextManagementOptions
            {
                MaxMessages = maxMessages,
                MaxEstimatedTokens = 1000000,
                KeepRecentMessages = keepRecent,
                CharactersPerToken = 4,
                MaxToolOutputCharactersInTranscript = 500
            }),
            Microsoft.Extensions.Options.Options.Create(new AiSettingsOptions { SummarizerTemperature = 0.1f }),
            NullLogger<ContextManager>.Instance);
    }

    private static List<ChatMessage> CreateHistory(int count)
    {
        List<ChatMessage> history = [];
        for (int index = 0; index < count; index++)
        {
            history.Add(new ChatMessage(index % 2 == 0 ? ChatRole.User : ChatRole.Assistant, $"message {index}"));
        }

        return history;
    }

    [Fact]
    public async Task PruneAsync_BelowThresholds_ReturnsHistoryUntouched()
    {
        ContextManager manager = CreateManager(maxMessages: 10, keepRecent: 4);
        List<ChatMessage> history = CreateHistory(6);

        IReadOnlyList<ChatMessage> result = await manager.PruneAsync(Guid.NewGuid(), history, CancellationToken.None);

        result
            .Should()
            .BeSameAs(history, "because no threshold is breached and no summarization is needed");
        _chatClient.Verify(
            client => client.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PruneAsync_AboveMessageThreshold_ReplacesOldMessagesWithCheckpoint()
    {
        ContextManager manager = CreateManager(maxMessages: 10, keepRecent: 4);

        IReadOnlyList<ChatMessage> result = await manager.PruneAsync(Guid.NewGuid(), CreateHistory(14), CancellationToken.None);

        result
            .Should()
            .HaveCount(5, "because the checkpoint replaces everything except the four most recent messages");
        result[0].Text
            .Should()
            .StartWith(ContextManager.CheckpointPrefix, "because the first message must be recognizable as a checkpoint")
            .And.Contain("checkpoint text");
        result[^1].Text
            .Should()
            .Be("message 13", "because the most recent message must be preserved verbatim");
        _publishEndpoint.Verify(
            endpoint => endpoint.Publish(It.Is<IndexTextCommand>(command => command.Text == "checkpoint text"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void FindSplitIndex_ToolResultAtBoundary_MovesBackToKeepToolCallPair()
    {
        List<ChatMessage> history =
        [
            new(ChatRole.User, "u1"),
            new(ChatRole.Assistant, [new FunctionCallContent("1", "read_file")]),
            new(ChatRole.Tool, [new FunctionResultContent("1", "ok")]),
            new(ChatRole.Assistant, "done")
        ];

        int split = ContextManager.FindSplitIndex(history, keepRecent: 2);

        split
            .Should()
            .Be(1, "because the tool result must stay together with the assistant message that requested it");
    }
}
