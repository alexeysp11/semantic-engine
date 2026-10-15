namespace Semantic.Engine.Workers.Tools.Workspace;

/// <summary>
/// A recoverable tool failure whose message is returned to the model so that it can correct the call.
/// </summary>
public sealed class ToolExecutionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ToolExecutionException"/> class.</summary>
    /// <param name="message">The error description shown to the model.</param>
    public ToolExecutionException(string message)
        : base(message)
    {
    }
}
