using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace Semantic.Engine.Client.Cli.Rendering;

/// <summary>
/// Converts C# source code into Spectre.Console markup with syntax colors.
/// </summary>
public static partial class CSharpHighlighter
{
    private const string CommentColor = "green";
    private const string StringColor = "darkorange";
    private const string KeywordColor = "blue";
    private const string TypeColor = "aqua";
    private const string NumberColor = "plum1";

    private static readonly HashSet<string> Keywords =
    [
        "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch", "char", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "get", "if", "implicit", "in", "init", "int",
        "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out",
        "override", "params", "private", "protected", "public", "readonly", "record", "ref", "required", "return",
        "sbyte", "sealed", "set", "short", "static", "string", "struct", "switch", "this", "throw", "true", "try",
        "typeof", "uint", "ulong", "using", "ushort", "var", "virtual", "void", "volatile", "while", "yield"
    ];

    /// <summary>
    /// Determines whether a fence language tag denotes C#.
    /// </summary>
    /// <param name="language">The lower-case language tag.</param>
    /// <returns><see langword="true"/> for <c>csharp</c>, <c>cs</c> and <c>c#</c>.</returns>
    public static bool IsCSharp(string language)
    {
        return language is "csharp" or "cs" or "c#";
    }

    /// <summary>
    /// Produces markup for C# source code. All user text is escaped.
    /// </summary>
    /// <param name="code">The source code.</param>
    /// <returns>Markup that can be passed to <see cref="Markup"/>.</returns>
    public static string ToMarkup(string code)
    {
        StringBuilder markup = new();
        int position = 0;

        foreach (Match match in TokenPattern().Matches(code))
        {
            if (match.Index > position)
            {
                markup.Append(Markup.Escape(code[position..match.Index]));
            }

            string? color = ColorOf(match);
            string escaped = Markup.Escape(match.Value);
            markup.Append(color is null ? escaped : $"[{color}]{escaped}[/]");
            position = match.Index + match.Length;
        }

        if (position < code.Length)
        {
            markup.Append(Markup.Escape(code[position..]));
        }

        return markup.ToString();
    }

    private static string? ColorOf(Match match)
    {
        if (match.Groups["comment"].Success)
        {
            return CommentColor;
        }

        if (match.Groups["string"].Success)
        {
            return StringColor;
        }

        if (match.Groups["number"].Success)
        {
            return NumberColor;
        }

        if (match.Groups["word"].Success)
        {
            string word = match.Value;
            if (Keywords.Contains(word))
            {
                return KeywordColor;
            }

            if (char.IsUpper(word[0]) && word.Length > 1)
            {
                return TypeColor;
            }
        }

        return null;
    }

    [GeneratedRegex(
        """(?<comment>//[^\n]*|/\*[\s\S]*?(\*/|$))|(?<string>\$?@?"(?:[^"\\\n]|\\.)*"?|'(?:[^'\\\n]|\\.)*'?)|(?<number>\b\d[\d_]*(\.\d+)?[fFdDmMlLuU]*\b)|(?<word>\b[A-Za-z_]\w*\b)""")]
    private static partial Regex TokenPattern();
}
