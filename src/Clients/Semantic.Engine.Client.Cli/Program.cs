using System.Text;
using Microsoft.Extensions.AI;

namespace Semantic.Engine.Client.Cli;

internal class Program
{
    private static async Task Main(string[] args)
    {
        const string ollamaEndpoint = "http://localhost:11434";
        const string modelId = "qwen2.5-coder:7b";

        OllamaChatClient chatClient = new(new Uri(ollamaEndpoint), modelId);

        // This list will accumulate the entire conversation state
        var conversationHistory = new List<ChatMessage>
        {
            new(ChatRole.System, "You are an expert C# architect inside Semantic.Engine. Remember the code context precisely.")
        };

        Console.WriteLine("======================================================================");
        Console.WriteLine($" Semantic.Engine.Cli - Persistent Context Session Enabled");
        Console.WriteLine("======================================================================");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nUser (Press Ctrl+Enter to submit):\n");
            Console.ResetColor();

            StringBuilder promptBuilder = new();

            while (true)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

                if (keyInfo.Key == ConsoleKey.Enter && (keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    Console.WriteLine();
                    break;
                }

                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    promptBuilder.AppendLine();
                    Console.WriteLine();
                    continue;
                }

                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (promptBuilder.Length > 0)
                    {
                        promptBuilder.Remove(promptBuilder.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    continue;
                }

                if (keyInfo.KeyChar != '\0')
                {
                    promptBuilder.Append(keyInfo.KeyChar);
                    Console.Write(keyInfo.KeyChar);
                }
            }

            string userPrompt = promptBuilder.ToString().Trim();

            if (string.IsNullOrWhiteSpace(userPrompt)) continue;
            if (userPrompt.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            // 1. Add the current user prompt to the persistent memory block
            conversationHistory.Add(new ChatMessage(ChatRole.User, userPrompt));

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\nAgent: ");
            Console.ResetColor();

            StringBuilder assistantResponseBuilder = new();

            try
            {
                // 2. Pass the WHOLE history object, not just a single string payload
                var responseChunks = chatClient.GetStreamingResponseAsync(conversationHistory);

                await foreach (var chunk in responseChunks)
                {
                    if (chunk.Text != null)
                    {
                        Console.Write(chunk.Text);
                        // Accumulate the incoming model stream tokens
                        assistantResponseBuilder.Append(chunk.Text);
                    }
                }

                Console.WriteLine();

                // 3. Save the full model response into history so it is remembered in the next turn
                conversationHistory.Add(new ChatMessage(ChatRole.Assistant, assistantResponseBuilder.ToString()));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[Error]: Streaming failed. Details: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
