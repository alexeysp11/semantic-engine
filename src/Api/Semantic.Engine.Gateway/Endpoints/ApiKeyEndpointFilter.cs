using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Semantic.Engine.Gateway.Options;

namespace Semantic.Engine.Gateway.Endpoints;

/// <summary>
/// Rejects requests that do not carry the configured API key.
/// </summary>
/// <remarks>
/// The key is compared in constant time. When no key is configured the filter lets every request pass.
/// </remarks>
public sealed class ApiKeyEndpointFilter(IOptions<SecurityOptions> options) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        string? expected = options.Value.ApiKey;
        if (string.IsNullOrEmpty(expected))
        {
            return await next(context);
        }

        string provided = context.HttpContext.Request.Headers[SecurityOptions.ApiKeyHeader].ToString();
        bool valid = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));

        return valid ? await next(context) : Results.Unauthorized();
    }
}
