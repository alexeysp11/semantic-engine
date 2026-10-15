using System.Text;

namespace Semantic.Engine.Orchestrator.Agent;

/// <summary>
/// Withholds text that may be a tool call written as JSON so that it is not streamed to the user.
/// </summary>
/// <remarks>
/// <para>
/// Small models often write a tool call as plain text. The text is needed by the Orchestrator but is noise for the user.
/// The filter passes ordinary text through immediately and holds back everything from the first line that starts with
/// <c>{</c> or <c>&lt;</c> outside a fenced code block. After the response is complete, <see cref="Complete"/> releases the
/// held text when it turned out not to be a tool call.
/// </para>
/// <para>The filter is not thread-safe; one instance serves one model response.</para>
/// </remarks>
public sealed class ToolCallStreamFilter
{
    private readonly StringBuilder _held = new();
    private bool _holding;
    private bool _atLineStart = true;
    private bool _inFence;
    private int _backtickRun;

    /// <summary>
    /// Processes a streamed fragment.
    /// </summary>
    /// <param name="fragment">The text fragment produced by the model.</param>
    /// <returns>The text that may be shown to the user now; empty while the filter withholds text.</returns>
    public string Push(string fragment)
    {
        StringBuilder visible = new();

        foreach (char character in fragment)
        {
            if (_holding)
            {
                _held.Append(character);
                continue;
            }

            if (_atLineStart && !_inFence && (character == '{' || character == '<'))
            {
                _holding = true;
                _held.Append(character);
                continue;
            }

            TrackFence(character);

            if (character == '\n')
            {
                _atLineStart = true;
            }
            else if (!char.IsWhiteSpace(character))
            {
                _atLineStart = false;
            }

            visible.Append(character);
        }

        return visible.ToString();
    }

    /// <summary>
    /// Finishes the response.
    /// </summary>
    /// <param name="containsToolCall">Whether the response contained a tool call that the Orchestrator executes.</param>
    /// <returns>The withheld text when the response is not a tool call; otherwise an empty string.</returns>
    public string Complete(bool containsToolCall)
    {
        string released = containsToolCall ? string.Empty : _held.ToString();
        _held.Clear();
        _holding = false;
        return released;
    }

    private void TrackFence(char character)
    {
        if (character == '`')
        {
            _backtickRun++;
            if (_backtickRun == 3)
            {
                _inFence = !_inFence;
                _backtickRun = 0;
            }

            return;
        }

        _backtickRun = 0;
    }
}
