using FluentAssertions;
using Semantic.Engine.Orchestrator.Agent;
using Semantic.Engine.Orchestrator.StateMachine;
using Xunit;

namespace Semantic.Engine.UnitTests.Orchestrator;

public sealed class ToolCallStreamFilterTests
{
    private static string Stream(ToolCallStreamFilter filter, params string[] fragments)
    {
        return string.Concat(fragments.Select(filter.Push));
    }

    [Fact]
    public void Push_PlainText_PassesThroughImmediately()
    {
        ToolCallStreamFilter filter = new();

        string visible = Stream(filter, "Hello ", "world");

        visible
            .Should()
            .Be("Hello world", "because ordinary text must not be delayed");
    }

    [Fact]
    public void Push_JsonToolCallAtLineStart_IsWithheld()
    {
        ToolCallStreamFilter filter = new();

        string visible = Stream(filter, "{\"name\":", " \"dotnet_build\", ", "\"arguments\": {}}");
        string released = filter.Complete(containsToolCall: true);

        visible
            .Should()
            .BeEmpty("because the user must not see raw tool call JSON");
        released
            .Should()
            .BeEmpty("because a confirmed tool call is never shown");
    }

    [Fact]
    public void Push_ProseBeforeToolCall_ShowsOnlyProse()
    {
        ToolCallStreamFilter filter = new();

        string visible = Stream(filter, "I will build it.\n", "{\"name\": \"dotnet_build\"}");

        visible
            .Should()
            .Be("I will build it.\n", "because prose before the call stays visible");
    }

    [Fact]
    public void Complete_HeldTextWasNotAToolCall_ReleasesIt()
    {
        ToolCallStreamFilter filter = new();
        Stream(filter, "{ not a tool call }");

        string released = filter.Complete(containsToolCall: false);

        released
            .Should()
            .Be("{ not a tool call }", "because text that is not a tool call must still reach the user");
    }

    [Fact]
    public void Push_BraceInsideCodeFence_IsNotWithheld()
    {
        ToolCallStreamFilter filter = new();

        string visible = Stream(filter, "```csharp\n", "{\n", "}\n", "```");

        visible
            .Should()
            .Be("```csharp\n{\n}\n```", "because braces of C# code inside a fence are ordinary text");
    }
}

public sealed class PendingToolCallTests
{
    [Fact]
    public void Matches_SnapshotWithSameCallId_ReturnsTrue()
    {
        string snapshot = PendingToolCall.Serialize(new PendingToolCall("call-1", "read_file", "{}"));

        PendingToolCall.Matches(snapshot, "call-1")
            .Should()
            .BeTrue("because the result answers the pending call");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Matches_EmptySnapshot_ReturnsFalse(string? snapshot)
    {
        PendingToolCall.Matches(snapshot, "call-1")
            .Should()
            .BeFalse("because no call is pending");
    }

    [Fact]
    public void Deserialize_RoundTripsSerializedCall()
    {
        PendingToolCall original = new("call-1", "dotnet_build", """{"project_path":"a"}""");

        PendingToolCall.Deserialize(PendingToolCall.Serialize(original))
            .Should()
            .Be(original, "because the watchdog relies on the snapshot to rebuild the call");
    }
}
