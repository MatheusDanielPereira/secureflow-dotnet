using SecureFlow.Api.Security;
using SecureFlow.Application.Abstractions;
using SecureFlow.Domain.Auditing;

namespace SecureFlow.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/audit-events",
                (int? take, IAuditLog auditLog, CancellationToken cancellationToken) =>
                    ListAsync(take, auditLog, cancellationToken))
            .RequireAuthorization(AuthorizationPolicies.Audit)
            .RequireRateLimiting("api")
            .WithName("ListAuditEvents")
            .WithTags("Audit")
            .Produces<IReadOnlyList<AuditEntry>>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static Task<IReadOnlyList<AuditEntry>> ListAsync(
        int? take,
        IAuditLog auditLog,
        CancellationToken cancellationToken)
    {
        return auditLog.ListAsync(take ?? 100, cancellationToken);
    }
}
