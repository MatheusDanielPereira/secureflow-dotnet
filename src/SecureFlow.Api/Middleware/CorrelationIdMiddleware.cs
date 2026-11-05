using System.Diagnostics;

namespace SecureFlow.Api.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context.Request);
        context.TraceIdentifier = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(HttpRequest request)
    {
        var candidate = request.Headers[HeaderName].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(candidate)
            && candidate.Length <= 64
            && candidate.All(character => char.IsLetterOrDigit(character) || character is '-' or '_'))
        {
            return candidate;
        }

        return Guid.NewGuid().ToString("N");
    }
}
