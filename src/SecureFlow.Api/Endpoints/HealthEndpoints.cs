using SecureFlow.Application.Abstractions;

namespace SecureFlow.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/healthz", (IClock clock) => Results.Ok(new
        {
            status = "healthy",
            service = "SecureFlow.Api",
            version = typeof(Program).Assembly.GetName().Version?.ToString(),
            timestampUtc = clock.UtcNow
        }))
            .AllowAnonymous()
            .WithName("Health")
            .WithTags("Operations")
            .Produces(StatusCodes.Status200OK);

        return endpoints;
    }
}
