using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Semantic.Engine.Contracts.Tools;
using Semantic.Engine.Workers.Tools.Options;
using Semantic.Engine.Workers.Tools.Workspace;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// Sends an HTTP request on behalf of the agent and returns the status code and the response body.
/// </summary>
/// <remarks>
/// Redirects are not followed and, unless explicitly enabled, targets that resolve to loopback or private
/// network addresses are rejected so that the agent cannot reach internal infrastructure.
/// </remarks>
public sealed class HttpRequestTool(
    IHttpClientFactory httpClientFactory,
    IOptions<ToolsWorkerOptions> options) : ITool
{
    /// <summary>The name of the <see cref="HttpClient"/> registered for this tool.</summary>
    public const string HttpClientName = "agent-http-tool";

    private static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "DELETE"];

    /// <inheritdoc />
    public string Name => ToolNames.HttpRequest;

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(Guid sessionId, JsonElement arguments, CancellationToken cancellationToken)
    {
        string method = ToolArguments.GetRequiredString(arguments, "method").ToUpperInvariant();
        string url = ToolArguments.GetRequiredString(arguments, "url");
        string? body = ToolArguments.GetOptionalString(arguments, "body");

        if (!AllowedMethods.Contains(method))
        {
            throw new ToolExecutionException($"The method '{method}' is not allowed. Allowed: {string.Join(", ", AllowedMethods)}.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ToolExecutionException("The argument 'url' must be an absolute http or https URL.");
        }

        await EnsureTargetIsAllowedAsync(uri, cancellationToken);

        using HttpRequestMessage request = new(new HttpMethod(method), uri);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        HttpClient client = httpClientFactory.CreateClient(HttpClientName);
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        int limit = options.Value.MaxOutputCharacters;
        if (responseBody.Length > limit)
        {
            responseBody = string.Concat(responseBody.AsSpan(0, limit), "\n...[truncated]");
        }

        string output = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{responseBody}";
        return new ToolResult(response.IsSuccessStatusCode, output);
    }

    private async Task EnsureTargetIsAllowedAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (options.Value.AllowPrivateNetworkTargets)
        {
            return;
        }

        IPAddress[] addresses = IPAddress.TryParse(uri.Host, out IPAddress? literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(address => !NetworkAddressPolicy.IsPublic(address)))
        {
            throw new ToolExecutionException($"The host '{uri.Host}' is not a public internet address and is blocked.");
        }
    }
}
