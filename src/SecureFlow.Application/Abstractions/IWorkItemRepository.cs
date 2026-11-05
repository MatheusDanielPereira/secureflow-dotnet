using SecureFlow.Domain.WorkItems;

namespace SecureFlow.Application.Abstractions;

public interface IWorkItemRepository
{
    Task AddAsync(WorkItem workItem, CancellationToken cancellationToken);

    Task<WorkItem?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItem>> ListAsync(
        WorkItemStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<int> CountAsync(WorkItemStatus? status, CancellationToken cancellationToken);

    Task UpdateAsync(
        WorkItem workItem,
        long expectedVersion,
        CancellationToken cancellationToken);
}
