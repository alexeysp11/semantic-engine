using System.Text;

namespace Semantic.Engine.Client.Cli.Input;

/// <summary>
/// Text state of the multi-line prompt editor. Contains no console calls so that it can be tested in isolation.
/// </summary>
public sealed class InputBuffer
{
    private const string TabReplacement = "    ";

    private readonly StringBuilder _text = new();

    /// <summary>Gets the current text.</summary>
    public string Text => _text.ToString();

    /// <summary>Gets a value indicating whether the buffer is empty.</summary>
    public bool IsEmpty => _text.Length == 0;

    /// <summary>
    /// Appends a typed or pasted character.
    /// </summary>
    /// <param name="character">The character to append. Tabs expand to spaces; control characters are ignored.</param>
    /// <returns>The text that must be echoed to the terminal; empty when the character was ignored.</returns>
    public string Append(char character)
    {
        if (character == '\t')
        {
            _text.Append(TabReplacement);
            return TabReplacement;
        }

        if (char.IsControl(character))
        {
            return string.Empty;
        }

        _text.Append(character);
        return character.ToString();
    }

    /// <summary>
    /// Appends a line break.
    /// </summary>
    public void AppendNewLine()
    {
        _text.Append('\n');
    }

    /// <summary>
    /// Removes the last character.
    /// </summary>
    /// <returns>
    /// The kind of character that was removed so that the caller can repair the terminal; the column to return to
    /// is provided when a line break was removed.
    /// </returns>
    public BackspaceResult Backspace()
    {
        if (_text.Length == 0)
        {
            return new BackspaceResult(BackspaceKind.None, 0);
        }

        char removed = _text[^1];
        _text.Length--;

        if (removed != '\n')
        {
            return new BackspaceResult(BackspaceKind.Character, 0);
        }

        int lastBreak = LastIndexOfNewLine();
        int column = _text.Length - (lastBreak + 1);
        return new BackspaceResult(BackspaceKind.NewLine, column);
    }

    /// <summary>
    /// Clears the buffer.
    /// </summary>
    public void Clear()
    {
        _text.Clear();
    }

    private int LastIndexOfNewLine()
    {
        for (int index = _text.Length - 1; index >= 0; index--)
        {
            if (_text[index] == '\n')
            {
                return index;
            }
        }

        return -1;
    }
}

/// <summary>
/// Describes what <see cref="InputBuffer.Backspace"/> removed.
/// </summary>
/// <param name="Kind">The kind of the removed element.</param>
/// <param name="ColumnAfterNewLine">The column of the caret on the previous line when a line break was removed.</param>
public readonly record struct BackspaceResult(BackspaceKind Kind, int ColumnAfterNewLine);

/// <summary>
/// The kind of element removed by a backspace.
/// </summary>
public enum BackspaceKind
{
    /// <summary>The buffer was empty.</summary>
    None = 0,

    /// <summary>A regular character was removed.</summary>
    Character = 1,

    /// <summary>A line break was removed.</summary>
    NewLine = 2
}
