namespace SecureFlow.Application.Common;

public sealed class ConcurrencyConflictException(Guid entityId)
    : Exception($"Work item '{entityId}' was modified by another request.")
{
    public Guid EntityId { get; } = entityId;
}
