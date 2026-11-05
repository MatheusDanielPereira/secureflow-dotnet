using System.Collections.Concurrent;

using SecureFlow.Application.Abstractions;
using SecureFlow.Application.Common;
using SecureFlow.Domain.WorkItems;

namespace SecureFlow.Infrastructure.Persistence;

public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly ConcurrentDictionary<Guid, WorkItem> _items = new();

    public Task AddAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_items.TryAdd(workItem.Id, workItem))
        {
            throw new InvalidOperationException($"Work item '{workItem.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<WorkItem?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items.TryGetValue(id, out var workItem);
        return Task.FromResult(workItem);
    }

    public Task<IReadOnlyList<WorkItem>> ListAsync(
        WorkItemStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = _items.Values
            .Where(item => status is null || item.Status == status)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Skip(skip)
            .Take(take)
            .ToArray();

        return Task.FromResult<IReadOnlyList<WorkItem>>(items);
    }

    public Task<int> CountAsync(WorkItemStatus? status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var count = _items.Values.Count(item => status is null || item.Status == status);
        return Task.FromResult(count);
    }

    public Task UpdateAsync(
        WorkItem workItem,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (true)
        {
            if (!_items.TryGetValue(workItem.Id, out var current))
            {
                throw new EntityNotFoundException("Work item", workItem.Id);
            }

            if (current.Version != expectedVersion)
            {
                throw new ConcurrencyConflictException(workItem.Id);
            }

            if (_items.TryUpdate(workItem.Id, workItem, current))
            {
                return Task.CompletedTask;
            }
        }
    }
}
