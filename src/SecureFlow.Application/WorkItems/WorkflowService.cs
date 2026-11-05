using SecureFlow.Application.Abstractions;
using SecureFlow.Application.Common;
using SecureFlow.Domain.Auditing;
using SecureFlow.Domain.WorkItems;

namespace SecureFlow.Application.WorkItems;

public sealed class WorkflowService(
    IWorkItemRepository repository,
    IAuditLog auditLog,
    IClock clock)
{
    public async Task<WorkItemResult> CreateAsync(
        CreateWorkItemCommand command,
        CancellationToken cancellationToken)
    {
        var workItem = WorkItem.Create(
            command.Title,
            command.Description,
            command.ActorId,
            clock.UtcNow);

        await repository.AddAsync(workItem, cancellationToken);
        await AppendAuditAsync(workItem, "work-item.created", command.ActorId, cancellationToken);

        return WorkItemResult.FromDomain(workItem);
    }

    public async Task<WorkItemResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var workItem = await GetRequiredAsync(id, cancellationToken);
        return WorkItemResult.FromDomain(workItem);
    }

    public async Task<PagedResult<WorkItemResult>> ListAsync(
        WorkItemStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (safePage - 1) * safePageSize;

        var items = await repository.ListAsync(
            status,
            skip,
            safePageSize,
            cancellationToken);
        var totalCount = await repository.CountAsync(status, cancellationToken);

        return new PagedResult<WorkItemResult>(
            items.Select(WorkItemResult.FromDomain).ToArray(),
            safePage,
            safePageSize,
            totalCount);
    }

    public async Task<WorkItemResult> SubmitAsync(
        Guid id,
        long expectedVersion,
        string actorId,
        CancellationToken cancellationToken)
    {
        var current = await GetRequiredAsync(id, cancellationToken);
        EnsureExpectedVersion(current, expectedVersion);
        var updated = current.Submit(actorId, clock.UtcNow);

        await repository.UpdateAsync(updated, expectedVersion, cancellationToken);
        await AppendAuditAsync(updated, "work-item.submitted", actorId, cancellationToken);

        return WorkItemResult.FromDomain(updated);
    }

    public async Task<WorkItemResult> ApproveAsync(
        Guid id,
        long expectedVersion,
        string actorId,
        CancellationToken cancellationToken)
    {
        var current = await GetRequiredAsync(id, cancellationToken);
        EnsureExpectedVersion(current, expectedVersion);
        var updated = current.Approve(actorId, clock.UtcNow);

        await repository.UpdateAsync(updated, expectedVersion, cancellationToken);
        await AppendAuditAsync(updated, "work-item.approved", actorId, cancellationToken);

        return WorkItemResult.FromDomain(updated);
    }

    public async Task<WorkItemResult> RejectAsync(
        Guid id,
        long expectedVersion,
        RejectWorkItemCommand command,
        CancellationToken cancellationToken)
    {
        var current = await GetRequiredAsync(id, cancellationToken);
        EnsureExpectedVersion(current, expectedVersion);
        var updated = current.Reject(command.ActorId, command.Reason, clock.UtcNow);

        await repository.UpdateAsync(updated, expectedVersion, cancellationToken);
        await AppendAuditAsync(updated, "work-item.rejected", command.ActorId, cancellationToken);

        return WorkItemResult.FromDomain(updated);
    }

    private async Task<WorkItem> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        return await repository.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("Work item", id);
    }

    private static void EnsureExpectedVersion(WorkItem workItem, long expectedVersion)
    {
        if (workItem.Version != expectedVersion)
        {
            throw new ConcurrencyConflictException(workItem.Id);
        }
    }

    private Task AppendAuditAsync(
        WorkItem workItem,
        string eventName,
        string actorId,
        CancellationToken cancellationToken)
    {
        var entry = new AuditEntry(
            Guid.NewGuid(),
            workItem.Id,
            eventName,
            actorId,
            clock.UtcNow,
            workItem.Version);

        return auditLog.AppendAsync(entry, cancellationToken);
    }
}
