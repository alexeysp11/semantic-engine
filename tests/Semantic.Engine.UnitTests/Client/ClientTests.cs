using FluentAssertions;
using Semantic.Engine.Client.Cli.Gateway;
using Semantic.Engine.Client.Cli.Input;
using Semantic.Engine.Client.Cli.Rendering;
using Xunit;

namespace Semantic.Engine.UnitTests.Client;

public sealed class ClientTests
{
    [Fact]
    public void InputBuffer_AppendsCharactersAndLineBreaks()
    {
        InputBuffer buffer = new();

        buffer.Append('a');
        buffer.AppendNewLine();
        buffer.Append('b');

        buffer.Text
            .Should()
            .Be("a\nb", "because Enter inserts a line break instead of submitting");
    }

    [Fact]
    public void InputBuffer_Tab_ExpandsToSpaces()
    {
        InputBuffer buffer = new();

        string echo = buffer.Append('\t');

        echo
            .Should()
            .Be("    ", "because pasted tabs must not break terminal layout");
    }

    [Fact]
    public void InputBuffer_BackspaceOverLineBreak_ReportsPreviousLineColumn()
    {
        InputBuffer buffer = new();
        foreach (char character in "abc")
        {
            buffer.Append(character);
        }

        buffer.AppendNewLine();
        BackspaceResult result = buffer.Backspace();

        result.Kind
            .Should()
            .Be(BackspaceKind.NewLine, "because the removed element was a line break");
        result.ColumnAfterNewLine
            .Should()
            .Be(3, "because the caret returns to the end of the previous line");
    }

    [Fact]
    public void InputBuffer_BackspaceOnEmptyBuffer_DoesNothing()
    {
        new InputBuffer().Backspace().Kind
            .Should()
            .Be(BackspaceKind.None, "because there is nothing to delete");
    }

    [Fact]
    public void MarkdownSegmenter_SplitsProseAndCode()
    {
        IReadOnlyList<MarkdownSegment> segments = MarkdownSegmenter.Split("Fixed:\n```csharp\nclass A {}\n```\nDone.");

        segments
            .Select(segment => (segment.Kind, segment.Language, segment.Content))
            .Should()
            .Equal(
                (MarkdownSegmentKind.Text, string.Empty, "Fixed:"),
                (MarkdownSegmentKind.Code, "csharp", "class A {}"),
                (MarkdownSegmentKind.Text, string.Empty, "Done."));
    }

    [Fact]
    public void MarkdownSegmenter_UnterminatedFence_ReturnsIncompleteCode()
    {
        IReadOnlyList<MarkdownSegment> segments = MarkdownSegmenter.Split("```csharp\nclass A {");

        segments
            .Should()
            .ContainSingle("because code must be highlighted while it is still streaming")
            .Which.Content.Should().Be("class A {");
    }

    [Fact]
    public void CSharpHighlighter_ColorsKeywordsStringsAndComments_AndEscapesMarkup()
    {
        string markup = CSharpHighlighter.ToMarkup("public string S = \"[x]\"; // note");

        markup
            .Should()
            .Contain("[blue]public[/]", "because keywords are blue")
            .And.Contain("[darkorange]\"[[x]]\"[/]", "because strings are orange and square brackets are escaped")
            .And.Contain("[green]// note[/]", "because comments are green");
    }

    [Fact]
    public void StreamEventParser_ParsesAllEventKinds()
    {
        AgentStreamEvent? token = StreamEventParser.Parse("token", """{"text":"hi"}""");
        AgentStreamEvent? status = StreamEventParser.Parse("status", """{"status":"Failed","detail":"boom"}""");
        AgentStreamEvent? done = StreamEventParser.Parse("done", "{}");
        AgentStreamEvent? unknown = StreamEventParser.Parse("other", "{}");

        token!.Text
            .Should()
            .Be("hi", "because token events carry text");
        status!.IsFailure
            .Should()
            .BeTrue("because a Failed status ends the turn with an error");
        done!.Kind
            .Should()
            .Be(AgentStreamEventKind.Done, "because the done event terminates the turn");
        unknown
            .Should()
            .BeNull("because unknown events are ignored for forward compatibility");
    }

    [Fact]
    public void StatusText_FormatsToolExecution()
    {
        string caption = StatusText.Format(new AgentStreamEvent
        {
            Kind = AgentStreamEventKind.Status,
            Status = "ExecutingTool",
            Detail = "dotnet_build"
        });

        caption
            .Should()
            .Be("[blue]Executing dotnet build...[/]", "because the spinner names the running compilation");
    }
}
