using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Semantic.Engine.Workers.Tools.Options;
using Semantic.Engine.Workers.Tools.Tools;
using Semantic.Engine.Workers.Tools.Workspace;
using Xunit;

namespace Semantic.Engine.UnitTests.Tools;

public sealed class WorkspaceToolsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "semantic-engine-tests", Guid.NewGuid().ToString("N"));
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly IOptions<ToolsWorkerOptions> _options;
    private readonly WorkspacePathResolver _resolver;

    public WorkspaceToolsTests()
    {
        _options = Microsoft.Extensions.Options.Options.Create(new ToolsWorkerOptions
        {
            WorkspaceRoot = _root,
            MaxFileSizeBytes = 1024,
            MaxListEntries = 50,
            MaxOutputCharacters = 1000
        });
        _resolver = new WorkspacePathResolver(_options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static JsonElement Args(object value) => JsonSerializer.SerializeToElement(value);

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("a/../../escape.txt")]
    [InlineData("..")]
    public void Resolve_PathEscapingWorkspace_Throws(string path)
    {
        Action resolve = () => _resolver.Resolve(_sessionId, path);

        resolve
            .Should()
            .Throw<ToolExecutionException>("because tools must never touch files outside the session workspace");
    }

    [Fact]
    public void Resolve_AbsolutePath_Throws()
    {
        string absolute = Path.Combine(Path.GetTempPath(), "x.txt");

        Action resolve = () => _resolver.Resolve(_sessionId, absolute);

        resolve
            .Should()
            .Throw<ToolExecutionException>("because absolute paths bypass the workspace boundary");
    }

    [Fact]
    public void Resolve_NestedRelativePath_StaysInsideWorkspace()
    {
        string resolved = _resolver.Resolve(_sessionId, "src/A.cs");

        resolved
            .Should()
            .StartWith(_resolver.GetSessionRoot(_sessionId), "because the resolved path belongs to the session workspace");
    }

    [Fact]
    public async Task WriteThenRead_RoundTripsContentAtomically()
    {
        WriteFileTool write = new(_resolver, _options);
        ReadFileTool read = new(_resolver, _options);

        ToolResult written = await write.ExecuteAsync(_sessionId, Args(new { path = "src/A.cs", content = "class A {}" }), CancellationToken.None);
        ToolResult readBack = await read.ExecuteAsync(_sessionId, Args(new { path = "src/A.cs" }), CancellationToken.None);

        written.Succeeded
            .Should()
            .BeTrue("because writing inside the workspace is allowed");
        readBack.Output
            .Should()
            .Be("class A {}", "because the written content must be read back unchanged");
        Directory.GetFiles(_resolver.GetSessionRoot(_sessionId), "*.tmp", SearchOption.AllDirectories)
            .Should()
            .BeEmpty("because the temporary file of the atomic write must be gone");
    }

    [Fact]
    public async Task Write_ContentOverSizeLimit_Throws()
    {
        WriteFileTool write = new(_resolver, _options);

        Func<Task> act = () => write.ExecuteAsync(_sessionId, Args(new { path = "big.txt", content = new string('x', 2048) }), CancellationToken.None);

        await act
            .Should()
            .ThrowAsync<ToolExecutionException>("because oversized files must be rejected");
    }

    [Fact]
    public async Task Patch_UniqueFragment_ReplacesIt()
    {
        await File.WriteAllTextAsync(_resolver.Resolve(_sessionId, "A.cs"), "class A { void M( { } }");
        PatchFileTool patch = new(_resolver);

        ToolResult result = await patch.ExecuteAsync(
            _sessionId,
            Args(new { path = "A.cs", old_text = "M( {", new_text = "M() {" }),
            CancellationToken.None);

        result.Succeeded
            .Should()
            .BeTrue("because the fragment occurs exactly once");
        (await File.ReadAllTextAsync(_resolver.Resolve(_sessionId, "A.cs")))
            .Should()
            .Be("class A { void M() { } }", "because only the fragment must be replaced");
    }

    [Fact]
    public async Task Patch_AmbiguousFragment_Throws()
    {
        await File.WriteAllTextAsync(_resolver.Resolve(_sessionId, "A.cs"), "x x");
        PatchFileTool patch = new(_resolver);

        Func<Task> act = () => patch.ExecuteAsync(_sessionId, Args(new { path = "A.cs", old_text = "x", new_text = "y" }), CancellationToken.None);

        await act
            .Should()
            .ThrowAsync<ToolExecutionException>("because an ambiguous patch could modify the wrong place");
    }

    [Fact]
    public async Task Patch_LfFragmentInCrlfFile_IsMatched()
    {
        await File.WriteAllTextAsync(_resolver.Resolve(_sessionId, "A.cs"), "a\r\nb\r\nc\r\n");
        PatchFileTool patch = new(_resolver);

        await patch.ExecuteAsync(_sessionId, Args(new { path = "A.cs", old_text = "a\nb", new_text = "a\nB" }), CancellationToken.None);

        (await File.ReadAllTextAsync(_resolver.Resolve(_sessionId, "A.cs")))
            .Should()
            .Be("a\r\nB\r\nc\r\n", "because the model line endings are adapted to the file line endings");
    }

    [Theory]
    [InlineData("a\nb\n", 0, "x", "x\na\nb\n")]
    [InlineData("a\nb\n", 1, "x", "a\nx\nb\n")]
    [InlineData("a\nb\n", 2, "x", "a\nb\nx\n")]
    [InlineData("a\r\nb", 1, "x\ny", "a\r\nx\r\ny\r\nb")]
    public void InsertAfterLine_InsertsAtRequestedPosition(string content, int line, string text, string expected)
    {
        string result = InsertAtLineTool.InsertAfterLine(content, line, text);

        result
            .Should()
            .Be(expected, "because the text must be inserted after the requested line preserving line endings");
    }

    [Fact]
    public void InsertAfterLine_LineOutsideFile_Throws()
    {
        Action act = () => InsertAtLineTool.InsertAfterLine("a\n", 5, "x");

        act
            .Should()
            .Throw<ToolExecutionException>("because inserting beyond the end of the file is a model error");
    }

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("::1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    public void NetworkAddressPolicy_ClassifiesAddresses(string address, bool expectedPublic)
    {
        NetworkAddressPolicy.IsPublic(IPAddress.Parse(address))
            .Should()
            .Be(expectedPublic, "because only publicly routable addresses may be contacted by the agent");
    }
}
