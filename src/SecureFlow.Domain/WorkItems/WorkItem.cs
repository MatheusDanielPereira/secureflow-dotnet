using SecureFlow.Domain.Common;

namespace SecureFlow.Domain.WorkItems;

public sealed record WorkItem
{
    private WorkItem(
        Guid id,
        string title,
        string description,
        string ownerId,
        WorkItemStatus status,
        string? decisionReason,
        string? decidedBy,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        long version)
    {
        Id = id;
        Title = title;
        Description = description;
        OwnerId = ownerId;
        Status = status;
        DecisionReason = decisionReason;
        DecidedBy = decidedBy;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        Version = version;
    }

    public Guid Id { get; init; }

    public string Title { get; init; }

    public string Description { get; init; }

    public string OwnerId { get; init; }

    public WorkItemStatus Status { get; init; }

    public string? DecisionReason { get; init; }

    public string? DecidedBy { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public long Version { get; init; }

    public static WorkItem Create(
        string? title,
        string? description,
        string? ownerId,
        DateTimeOffset now)
    {
        var normalizedTitle = NormalizeRequired(title, "title", 3, 120);
        var normalizedDescription = (description ?? string.Empty).Trim();
        var normalizedOwner = NormalizeRequired(ownerId, "owner", 1, 200);

        if (normalizedDescription.Length > 2_000)
        {
            throw new DomainException(
                "validation.description_too_long",
                "Description must not exceed 2,000 characters.");
        }

        return new WorkItem(
            Guid.NewGuid(),
            normalizedTitle,
            normalizedDescription,
            normalizedOwner,
            WorkItemStatus.Draft,
            null,
            null,
            now,
            now,
            1);
    }

    public WorkItem Submit(string? actorId, DateTimeOffset now)
    {
        EnsureStatus(WorkItemStatus.Draft, "submit");
        EnsureOwner(actorId);

        return this with
        {
            Status = WorkItemStatus.Submitted,
            UpdatedAtUtc = now,
            Version = Version + 1
        };
    }

    public WorkItem Approve(string? actorId, DateTimeOffset now)
    {
        EnsureStatus(WorkItemStatus.Submitted, "approve");
        var normalizedActor = NormalizeRequired(actorId, "actor", 1, 200);
        EnsureSeparationOfDuties(normalizedActor);

        return this with
        {
            Status = WorkItemStatus.Approved,
            DecidedBy = normalizedActor,
            DecisionReason = null,
            UpdatedAtUtc = now,
            Version = Version + 1
        };
    }

    public WorkItem Reject(string? actorId, string? reason, DateTimeOffset now)
    {
        EnsureStatus(WorkItemStatus.Submitted, "reject");
        var normalizedActor = NormalizeRequired(actorId, "actor", 1, 200);
        var normalizedReason = NormalizeRequired(reason, "reason", 3, 500);
        EnsureSeparationOfDuties(normalizedActor);

        return this with
        {
            Status = WorkItemStatus.Rejected,
            DecidedBy = normalizedActor,
            DecisionReason = normalizedReason,
            UpdatedAtUtc = now,
            Version = Version + 1
        };
    }

    private static string NormalizeRequired(
        string? value,
        string field,
        int minimumLength,
        int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (normalized.Length < minimumLength || normalized.Length > maximumLength)
        {
            throw new DomainException(
                $"validation.{field}_length",
                $"{char.ToUpperInvariant(field[0]) + field[1..]} must contain between {minimumLength} and {maximumLength} characters.");
        }

        return normalized;
    }

    private void EnsureOwner(string? actorId)
    {
        var normalizedActor = NormalizeRequired(actorId, "actor", 1, 200);

        if (!string.Equals(OwnerId, normalizedActor, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                "authorization.owner_required",
                "Only the work item owner can submit it.");
        }
    }

    private void EnsureSeparationOfDuties(string actorId)
    {
        if (string.Equals(OwnerId, actorId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                "authorization.separation_of_duties",
                "The work item owner cannot approve or reject their own request.");
        }
    }

    private void EnsureStatus(WorkItemStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainException(
                "workflow.invalid_transition",
                $"Cannot {action} a work item in the '{Status}' state.");
        }
    }
}
