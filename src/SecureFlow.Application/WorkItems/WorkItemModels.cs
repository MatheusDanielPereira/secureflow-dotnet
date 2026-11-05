using SecureFlow.Domain.WorkItems;

namespace SecureFlow.Application.WorkItems;

public sealed record CreateWorkItemCommand(string? Title, string? Description, string ActorId);

public sealed record RejectWorkItemCommand(string? Reason, string ActorId);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record WorkItemResult(
    Guid Id,
    string Title,
    string Description,
    string OwnerId,
    WorkItemStatus Status,
    string? DecisionReason,
    string? DecidedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    long Version)
{
    public static WorkItemResult FromDomain(WorkItem workItem) => new(
        workItem.Id,
        workItem.Title,
        workItem.Description,
        workItem.OwnerId,
        workItem.Status,
        workItem.DecisionReason,
        workItem.DecidedBy,
        workItem.CreatedAtUtc,
        workItem.UpdatedAtUtc,
        workItem.Version);
}
