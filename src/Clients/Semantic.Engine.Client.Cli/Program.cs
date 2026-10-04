using Microsoft.Extensions.AI;

namespace Semantic.Engine.Client.Cli;

internal class Program
{
    private static async Task Main(string[] args)
    {
        const string ollamaEndpoint = "http://localhost:11434";
        const string modelId = "llama3.2";

        // Initialize the client using the official Microsoft.Extensions.AI abstraction
        IChatClient chatClient = new OllamaChatClient(new Uri(ollamaEndpoint), modelId);

        Console.WriteLine("========================================================");
        Console.WriteLine($" Semantic Engine Orchestrator CLI - Connected to {modelId}");
        Console.WriteLine(" Type your prompt and press Enter. Type 'exit' to quit. ");
        Console.WriteLine("========================================================");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nUser: ");
            Console.ResetColor();

            string? userPrompt = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                continue;
            }

            if (userPrompt.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Agent: ");
            Console.ResetColor();

            try
            {
                // Fix: In the stable API, the streaming method is 'GetStreamingResponseAsync'
                // It yields chunks of type 'ChatResponseUpdate'
                var responseChunks = chatClient.GetStreamingResponseAsync(userPrompt);

                await foreach (var chunk in responseChunks)
                {
                    if (chunk.Text != null)
                    {
                        Console.Write(chunk.Text);
                    }
                }

                Console.WriteLine();
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
