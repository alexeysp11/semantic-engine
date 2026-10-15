using FluentAssertions;
using Semantic.Engine.Orchestrator.Agent;
using Xunit;

namespace Semantic.Engine.UnitTests.Orchestrator;

public sealed class TextToolCallParserTests
{
    private static bool IsKnown(string name) => name is "read_file" or "write_file";

    [Fact]
    public void TryParse_BareJsonCall_ExtractsNameAndArguments()
    {
        bool parsed = TextToolCallParser.TryParse("""{"name":"read_file","arguments":{"path":"A.cs"}}""", IsKnown, out ParsedToolCall call);

        parsed
            .Should()
            .BeTrue("because a JSON object with a known tool name is a tool call");
        call.Name
            .Should()
            .Be("read_file", "because the name property selects the tool");
        call.ArgumentsJson
            .Should()
            .Contain("A.cs", "because the arguments object must be preserved verbatim");
    }

    [Fact]
    public void TryParse_CallInsideToolCallTagsWithProse_StripsWrappers()
    {
        string text = """
            I will read the file.
            <tool_call>
            {"name": "read_file", "arguments": {"path": "A.cs"}}
            </tool_call>
            """;

        bool parsed = TextToolCallParser.TryParse(text, IsKnown, out ParsedToolCall call);

        parsed
            .Should()
            .BeTrue("because small models wrap calls in tool_call tags");
        call.RemainingText
            .Should()
            .Be("I will read the file.", "because the call and its wrapper tags must be removed from the visible text");
    }

    [Fact]
    public void TryParse_ParametersAlias_IsAccepted()
    {
        bool parsed = TextToolCallParser.TryParse("""{"name":"write_file","parameters":{"path":"a","content":"b"}}""", IsKnown, out ParsedToolCall call);

        parsed
            .Should()
            .BeTrue("because some models use 'parameters' instead of 'arguments'");
        call.ArgumentsJson
            .Should()
            .Contain("content", "because the aliased object is the argument payload");
    }

    [Fact]
    public void TryParse_UnknownToolName_IsNotACall()
    {
        bool parsed = TextToolCallParser.TryParse("""{"name":"rm_rf","arguments":{}}""", IsKnown, out _);

        parsed
            .Should()
            .BeFalse("because unknown tool names must not be treated as calls");
    }

    [Fact]
    public void TryParse_CSharpCodeWithBraces_IsNotACall()
    {
        bool parsed = TextToolCallParser.TryParse("public class A { public int B { get; set; } }", IsKnown, out _);

        parsed
            .Should()
            .BeFalse("because ordinary C# code contains braces but no JSON tool call");
    }

    [Fact]
    public void TryParse_BracesInsideStrings_DoNotBreakMatching()
    {
        bool parsed = TextToolCallParser.TryParse("""{"name":"write_file","arguments":{"path":"a.cs","content":"class A { }"}}""", IsKnown, out ParsedToolCall call);

        parsed
            .Should()
            .BeTrue("because braces inside JSON strings must not affect brace matching");
        call.ArgumentsJson
            .Should()
            .Contain("class A { }", "because the string content must stay intact");
    }
}
