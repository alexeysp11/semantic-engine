namespace Semantic.Engine.Client.Cli.Input;

/// <summary>
/// Reads a multi-line prompt from the keyboard.
/// </summary>
/// <remarks>
/// Enter inserts a line break and <b>Ctrl+Enter</b> submits the prompt, so that multi-line C# snippets can be pasted
/// without being submitted halfway. The loop uses <see cref="Console.ReadKey(bool)"/> with interception and echoes
/// the characters itself.
/// </remarks>
public static class MultiLinePromptReader
{
    /// <summary>
    /// Reads one prompt.
    /// </summary>
    /// <returns>The entered text without surrounding whitespace.</returns>
    public static string Read()
    {
        InputBuffer buffer = new();

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                Console.WriteLine();
                return buffer.Text.Trim();
            }

            if (key.Key == ConsoleKey.Enter)
            {
                buffer.AppendNewLine();
                Console.WriteLine();
                continue;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                EraseLastCharacter(buffer);
                continue;
            }

            Console.Write(buffer.Append(key.KeyChar));
        }
    }

    private static void EraseLastCharacter(InputBuffer buffer)
    {
        BackspaceResult result = buffer.Backspace();

        switch (result.Kind)
        {
            case BackspaceKind.Character:
                Console.Write("\b \b");
                break;
            case BackspaceKind.NewLine:
                Console.SetCursorPosition(result.ColumnAfterNewLine, Math.Max(0, Console.CursorTop - 1));
                break;
        }
    }
}
