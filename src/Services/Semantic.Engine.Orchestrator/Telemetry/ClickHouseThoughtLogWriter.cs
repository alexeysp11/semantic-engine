using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Semantic.Engine.Orchestrator.Options;

namespace Semantic.Engine.Orchestrator.Telemetry;

/// <summary>
/// Writes thought log entries into ClickHouse through its HTTP interface using the <c>JSONEachRow</c> format.
/// </summary>
public sealed class ClickHouseThoughtLogWriter(
    HttpClient httpClient,
    IOptions<ClickHouseOptions> options,
    ILogger<ClickHouseThoughtLogWriter> logger) : IThoughtLogWriter
{
    /// <inheritdoc />
    public async Task WriteAsync(ThoughtLogEntry entry, CancellationToken cancellationToken)
    {
        ClickHouseOptions settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        try
        {
            string row = JsonSerializer.Serialize(new
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                session_id = entry.SessionId,
                step_type = entry.StepType,
                tool_name = entry.ToolName,
                content = entry.Content,
                duration_ms = entry.DurationMs
            });

            string query = Uri.EscapeDataString($"INSERT INTO {settings.Database}.thought_logs FORMAT JSONEachRow");
            using HttpRequestMessage request = new(HttpMethod.Post, $"/?query={query}")
            {
                Content = new StringContent(row, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.User}:{settings.Password}")));

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("ClickHouse rejected a thought log entry: {Status} {Body}", response.StatusCode, body);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Thought log entry for session {SessionId} was not stored", entry.SessionId);
        }
    }
}
