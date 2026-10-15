using System.Text.Json;

namespace Semantic.Engine.Orchestrator.Agent;

/// <summary>
/// Extracts a tool call that the model wrote as plain JSON text instead of a native function call.
/// </summary>
/// <remarks>
/// Small local models frequently emit <c>{"name": "...", "arguments": {...}}</c> inside the text stream,
/// optionally wrapped in <c>&lt;tool_call&gt;</c> tags or a markdown fence. The parser recovers such calls.
/// </remarks>
public static class TextToolCallParser
{
    /// <summary>
    /// Tries to find a tool call object inside a model response.
    /// </summary>
    /// <param name="text">The full text of the response.</param>
    /// <param name="isKnownTool">A predicate that tells whether a tool name is declared.</param>
    /// <param name="result">The parsed call when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a call to a known tool was found.</returns>
    public static bool TryParse(string text, Func<string, bool> isKnownTool, out ParsedToolCall result)
    {
        result = default!;
        int searchFrom = 0;

        while (searchFrom < text.Length)
        {
            int start = text.IndexOf('{', searchFrom);
            if (start < 0)
            {
                return false;
            }

            int end = FindMatchingBrace(text, start);
            if (end < 0)
            {
                return false;
            }

            if (TryParseCandidate(text.AsSpan(start, end - start + 1), isKnownTool, out string name, out string argumentsJson))
            {
                result = new ParsedToolCall(name, argumentsJson, StripCall(text, start, end));
                return true;
            }

            searchFrom = start + 1;
        }

        return false;
    }

    private static bool TryParseCandidate(
        ReadOnlySpan<char> candidate,
        Func<string, bool> isKnownTool,
        out string name,
        out string argumentsJson)
    {
        name = string.Empty;
        argumentsJson = string.Empty;

        try
        {
            using JsonDocument document = JsonDocument.Parse(candidate.ToString());
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("name", out JsonElement nameElement)
                || nameElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            string toolName = nameElement.GetString()!;
            if (!isKnownTool(toolName))
            {
                return false;
            }

            JsonElement arguments = default;
            bool hasArguments = root.TryGetProperty("arguments", out arguments)
                                || root.TryGetProperty("parameters", out arguments);
            if (hasArguments && arguments.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            name = toolName;
            argumentsJson = hasArguments ? arguments.GetRawText() : "{}";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int FindMatchingBrace(string text, int start)
    {
        int depth = 0;
        bool inString = false;

        for (int index = start; index < text.Length; index++)
        {
            char current = text[index];

            if (inString)
            {
                if (current == '\\')
                {
                    index++;
                }
                else if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (current)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return index;
                    }

                    break;
            }
        }

        return -1;
    }

    private static string StripCall(string text, int start, int end)
    {
        string remaining = string.Concat(text.AsSpan(0, start), text.AsSpan(end + 1));
        remaining = remaining
            .Replace("<tool_call>", string.Empty, StringComparison.Ordinal)
            .Replace("</tool_call>", string.Empty, StringComparison.Ordinal)
            .Replace("```json", string.Empty, StringComparison.Ordinal)
            .Replace("```", string.Empty, StringComparison.Ordinal);
        return remaining.Trim();
    }
}

/// <summary>
/// A tool call recovered from model text.
/// </summary>
/// <param name="Name">The tool name.</param>
/// <param name="ArgumentsJson">The arguments serialized as a JSON object.</param>
/// <param name="RemainingText">The response text with the call and its wrappers removed.</param>
public sealed record ParsedToolCall(string Name, string ArgumentsJson, string RemainingText);
