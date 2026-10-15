using FluentAssertions;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Orchestrator.Tools;
using Xunit;

namespace Semantic.Engine.UnitTests.Orchestrator;

public sealed class ToolCallGuardTests
{
    [Fact]
    public void Validate_ValidCall_IsAllowed()
    {
        ToolCallValidationResult result = ToolCallGuard.Validate(ToolNames.ReadFile, """{"path":"src/A.cs"}""");

        result.IsAllowed
            .Should()
            .BeTrue("because a known tool with all required arguments must pass send-safety validation");
    }

    [Fact]
    public void Validate_UnknownTool_IsRejectedWithToolList()
    {
        ToolCallValidationResult result = ToolCallGuard.Validate("format_disk", "{}");

        result.IsAllowed
            .Should()
            .BeFalse("because only tools from the catalog may be executed");
        result.RejectionReason
            .Should()
            .Contain(ToolNames.ReadFile, "because the model must learn which tools exist to correct itself");
    }

    [Theory]
    [InlineData("""{"path":"../secret.txt"}""")]
    [InlineData("""{"path":"a/../../secret.txt"}""")]
    [InlineData("""{"path":"C:\\Windows\\system.ini"}""")]
    [InlineData("""{"path":"/etc/passwd"}""")]
    public void Validate_PathOutsideWorkspace_IsRejected(string argumentsJson)
    {
        ToolCallValidationResult result = ToolCallGuard.Validate(ToolNames.ReadFile, argumentsJson);

        result.IsAllowed
            .Should()
            .BeFalse("because paths that leave the workspace must never reach the Tool Worker");
    }

    [Fact]
    public void Validate_MissingRequiredArgument_IsRejected()
    {
        ToolCallValidationResult result = ToolCallGuard.Validate(ToolNames.WriteFile, """{"path":"a.cs"}""");

        result.RejectionReason
            .Should()
            .Contain("content", "because the rejection must name the missing argument");
    }

    [Fact]
    public void Validate_MalformedJson_IsRejected()
    {
        ToolCallValidationResult result = ToolCallGuard.Validate(ToolNames.ReadFile, "{path:");

        result.IsAllowed
            .Should()
            .BeFalse("because arguments that are not JSON cannot be executed");
    }
}
