using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Semantic.Engine.Contracts.Commands;
using Semantic.Engine.Contracts.Events;
using Semantic.Engine.Contracts.Sagas;
using Semantic.Engine.Orchestrator.Context;
using Semantic.Engine.Orchestrator.StateMachine;
using Xunit;

namespace Semantic.Engine.UnitTests.Orchestrator;

public sealed class AgentSessionStateMachineTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _provider = new ServiceCollection()
            .AddMassTransitTestHarness(configurator =>
                configurator.AddSagaStateMachine<AgentSessionStateMachine, AgentSessionState>().InMemoryRepository())
            .BuildServiceProvider(true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    private ISagaStateMachineTestHarness<AgentSessionStateMachine, AgentSessionState> Saga =>
        _harness.GetSagaStateMachineHarness<AgentSessionStateMachine, AgentSessionState>();

    private async Task<Guid> StartSessionAsync()
    {
        Guid sessionId = Guid.NewGuid();
        await _harness.Bus.Publish(new StartAgentSessionCommand { SessionId = sessionId, Prompt = "fix my code" });
        (await Saga.Exists(sessionId, state => state.Thinking))
            .Should()
            .NotBeNull("because a started session must enter the Thinking state");
        return sessionId;
    }

    [Fact]
    public async Task StartSession_PublishesFirstTurnAndIndexesPrompt()
    {
        Guid sessionId = await StartSessionAsync();

        (await _harness.Published.Any<RunAgentTurnCommand>(published => published.Context.Message.SessionId == sessionId))
            .Should()
            .BeTrue("because the saga must request the first reasoning turn");
        (await _harness.Published.Any<IndexTextCommand>(published => published.Context.Message.Text == "fix my code"))
            .Should()
            .BeTrue("because the user prompt is memorized in the vector store");
    }

    [Fact]
    public async Task TurnWithToolCall_FreezesSessionAndPublishesToolCommand()
    {
        Guid sessionId = await StartSessionAsync();

        await _harness.Bus.Publish(new AgentTurnCompletedEvent
        {
            SessionId = sessionId,
            ConversationHistoryJson = ConversationHistory.Empty,
            ToolCallId = "call-1",
            ToolName = "dotnet_build",
            ArgumentsJson = "{}"
        });

        (await Saga.Exists(sessionId, state => state.AwaitingTool))
            .Should()
            .NotBeNull("because the agent waits for the tool result");
        (await _harness.Published.Any<ExecuteToolCommand>(published => published.Context.Message.ToolCallId == "call-1"))
            .Should()
            .BeTrue("because the requested tool call must be published to the Tool Worker");
    }

    [Fact]
    public async Task ToolResult_ResumesReasoning()
    {
        Guid sessionId = await StartSessionAsync();
        await _harness.Bus.Publish(new AgentTurnCompletedEvent
        {
            SessionId = sessionId,
            ConversationHistoryJson = ConversationHistory.Empty,
            ToolCallId = "call-1",
            ToolName = "dotnet_build",
            ArgumentsJson = "{}"
        });
        await Saga.Exists(sessionId, state => state.AwaitingTool);

        await _harness.Bus.Publish(new ToolExecutedEvent
        {
            SessionId = sessionId,
            ToolCallId = "call-1",
            ToolName = "dotnet_build",
            Succeeded = false,
            Output = "error CS1002: ; expected"
        });

        (await Saga.Exists(sessionId, state => state.Thinking))
            .Should()
            .NotBeNull("because the tool result unfreezes the agent");
        (await _harness.Published.Any<RunAgentTurnCommand>(published =>
                published.Context.Message.ConversationHistoryJson.Contains("CS1002", StringComparison.Ordinal)))
            .Should()
            .BeTrue("because the compiler error must reach the model in the next turn");
    }

    [Fact]
    public async Task ToolResultWithUnknownCallId_IsIgnored()
    {
        Guid sessionId = await StartSessionAsync();
        await _harness.Bus.Publish(new AgentTurnCompletedEvent
        {
            SessionId = sessionId,
            ConversationHistoryJson = ConversationHistory.Empty,
            ToolCallId = "call-1",
            ToolName = "read_file",
            ArgumentsJson = """{"path":"a"}"""
        });
        await Saga.Exists(sessionId, state => state.AwaitingTool);

        await _harness.Bus.Publish(new ToolExecutedEvent { SessionId = sessionId, ToolCallId = "stale", ToolName = "read_file", Output = "x" });
        await _harness.InactivityTask;

        (await Saga.Exists(sessionId, state => state.AwaitingTool))
            .Should()
            .NotBeNull("because a result of a different call must not unfreeze the agent");
    }

    [Fact]
    public async Task FinalTurn_ReturnsToActiveAndPublishesFinalToken()
    {
        Guid sessionId = await StartSessionAsync();

        await _harness.Bus.Publish(new AgentTurnCompletedEvent
        {
            SessionId = sessionId,
            ConversationHistoryJson = ConversationHistory.Empty
        });

        (await Saga.Exists(sessionId, state => state.Active))
            .Should()
            .NotBeNull("because a turn without a tool call completes the exchange");
        (await _harness.Published.Any<AgentTokenGeneratedEvent>(published => published.Context.Message.IsFinal))
            .Should()
            .BeTrue("because the client is told that the turn is finished");
    }

    [Fact]
    public async Task PromptWhileThinking_IsRejectedAsBusy()
    {
        Guid sessionId = await StartSessionAsync();

        await _harness.Bus.Publish(new SubmitAgentPromptCommand { SessionId = sessionId, Prompt = "second" });

        (await _harness.Published.Any<AgentStatusChangedEvent>(published =>
                published.Context.Message.Status == AgentStatus.Failed))
            .Should()
            .BeTrue("because a prompt must not interleave with a running turn");
    }

    [Fact]
    public async Task SearchMemoryToolCall_IsRoutedToDataWorker()
    {
        Guid sessionId = await StartSessionAsync();

        await _harness.Bus.Publish(new AgentTurnCompletedEvent
        {
            SessionId = sessionId,
            ConversationHistoryJson = ConversationHistory.Empty,
            ToolCallId = "call-2",
            ToolName = "search_memory",
            ArgumentsJson = """{"query":"task"}"""
        });

        (await _harness.Published.Any<SearchMemoryCommand>(published => published.Context.Message.ToolCallId == "call-2"))
            .Should()
            .BeTrue("because memory search is executed by the Data Worker, not by the Tool Worker");
        (await _harness.Published.Any<ExecuteToolCommand>(published => published.Context.Message.ToolCallId == "call-2"))
            .Should()
            .BeFalse("because the Tool Worker does not own the memory store");
    }
}
