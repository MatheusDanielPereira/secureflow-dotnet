namespace SecureFlow.Domain.Auditing;

public sealed record AuditEntry(
    Guid Id,
    Guid WorkItemId,
    string EventName,
    string ActorId,
    DateTimeOffset OccurredAtUtc,
    long WorkItemVersion);
